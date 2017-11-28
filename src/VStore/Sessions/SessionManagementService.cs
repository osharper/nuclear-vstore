using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Caching.Memory;

using Newtonsoft.Json;

using NuClear.VStore.DataContract;
using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Sessions;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Events;
using NuClear.VStore.Http;
using NuClear.VStore.Json;
using NuClear.VStore.Kafka;
using NuClear.VStore.Options;
using NuClear.VStore.Prometheus;
using NuClear.VStore.S3;
using NuClear.VStore.Sessions.ContentValidation;
using NuClear.VStore.Sessions.ContentValidation.Errors;
using NuClear.VStore.Sessions.UploadParams;
using NuClear.VStore.Templates;

using Prometheus.Client;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionManagementService
    {
        private static readonly IReadOnlyDictionary<FileFormat, string> ContentTypesMap =
            new Dictionary<FileFormat, string>
                {
                    { FileFormat.Chm, "application/vnd.ms-htmlhelp" },
                    { FileFormat.Svg, "image/svg+xml" },
                    { FileFormat.Pdf, "application/pdf" }
                };

        private readonly TimeSpan _sessionExpiration;
        private readonly Uri _fileStorageEndpointUri;
        private readonly string _filesBucketName;
        private readonly string _sessionsTopicName;
        private readonly ICephS3Client _cephS3Client;
        private readonly SessionStorageReader _sessionStorageReader;
        private readonly TemplatesStorageReader _templatesStorageReader;
        private readonly EventSender _eventSender;
        private readonly IMemoryCache _memoryCache;
        private readonly Counter _uploadedBinariesMetric;
        private readonly Counter _createdSessionsMetric;

        public SessionManagementService(
            CephOptions cephOptions,
            VStoreOptions vstoreOptions,
            KafkaOptions kafkaOptions,
            ICephS3Client cephS3Client,
            SessionStorageReader sessionStorageReader,
            TemplatesStorageReader templatesStorageReader,
            EventSender eventSender,
            MetricsProvider metricsProvider,
            IMemoryCache memoryCache)
        {
            _sessionExpiration = vstoreOptions.SessionExpiration;
            _fileStorageEndpointUri = vstoreOptions.FileStorageEndpoint;
            _filesBucketName = cephOptions.FilesBucketName;
            _sessionsTopicName = kafkaOptions.SessionEventsTopic;
            _cephS3Client = cephS3Client;
            _sessionStorageReader = sessionStorageReader;
            _templatesStorageReader = templatesStorageReader;
            _eventSender = eventSender;
            _memoryCache = memoryCache;
            _uploadedBinariesMetric = metricsProvider.GetUploadedBinariesMetric();
            _createdSessionsMetric = metricsProvider.GetCreatedSessionsMetric();
        }

        public async Task<SessionContext> GetSessionContext(Guid sessionId)
        {
            var (sessionDescriptor, authorInfo, expiresAt) = await _sessionStorageReader.GetSessionDescriptor(sessionId);
            var templateDescriptor = await _templatesStorageReader.GetTemplateDescriptor(sessionDescriptor.TemplateId, sessionDescriptor.TemplateVersionId);

            return new SessionContext(
                templateDescriptor.Id,
                templateDescriptor,
                sessionDescriptor.Language,
                authorInfo,
                expiresAt);
        }

        public async Task Setup(Guid sessionId, long templateId, string templateVersionId, Language language, AuthorInfo authorInfo)
        {
            if (language == Language.Unspecified)
            {
                throw new SessionCannotBeCreatedException("Language must be explicitly specified.");
            }

            var templateDescriptor = await _templatesStorageReader.GetTemplateDescriptor(templateId, templateVersionId);
            var sessionDescriptor = new SessionDescriptor
                                        {
                                            TemplateId = templateDescriptor.Id,
                                            TemplateVersionId = templateDescriptor.VersionId,
                                            Language = language,
                                            BinaryElementTemplateCodes = templateDescriptor.GetBinaryElementTemplateCodes()
                                        };
            var request = new PutObjectRequest
                              {
                                  BucketName = _filesBucketName,
                                  Key = sessionId.AsS3ObjectKey(Tokens.SessionPostfix),
                                  CannedACL = S3CannedACL.PublicRead,
                                  ContentType = ContentType.Json,
                                  ContentBody = JsonConvert.SerializeObject(sessionDescriptor, SerializerSettings.Default)
                              };

            var expiresAt = SessionDescriptor.CurrentTime().Add(_sessionExpiration);
            var metadataWrapper = MetadataCollectionWrapper.For(request.Metadata);
            metadataWrapper.Write(MetadataElement.ExpiresAt, expiresAt);
            metadataWrapper.Write(MetadataElement.Author, authorInfo.Author);
            metadataWrapper.Write(MetadataElement.AuthorLogin, authorInfo.AuthorLogin);
            metadataWrapper.Write(MetadataElement.AuthorName, authorInfo.AuthorName);

            await _eventSender.SendAsync(_sessionsTopicName, new SessionCreatingEvent(sessionId, expiresAt));

            await _cephS3Client.PutObjectAsync(request);
            _createdSessionsMetric.Inc();
        }

        public async Task<MultipartUploadSession> InitiateMultipartUpload(
            Guid sessionId,
            string fileName,
            string contentType,
            long fileLength,
            int templateCode)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new MissingFilenameException($"Filename has not been provided for the item '{templateCode}'");
            }

            var (sessionDescriptor, _, expiresAt) = await _sessionStorageReader.GetSessionDescriptor(sessionId);
            if (sessionDescriptor.BinaryElementTemplateCodes.All(x => x != templateCode))
            {
                throw new InvalidTemplateException(
                          $"Binary content is not expected for the item '{templateCode}' within template '{sessionDescriptor.TemplateId}' " +
                          $"with version Id '{sessionDescriptor.TemplateVersionId}'.");
            }

            var elementDescriptor = await GetElementDescriptor(sessionDescriptor.TemplateId, sessionDescriptor.TemplateVersionId, templateCode);
            EnsureFileMetadataIsValid(elementDescriptor, fileLength, sessionDescriptor.Language, fileName);

            var fileKey = Guid.NewGuid().ToString();
            var key = sessionId.AsS3ObjectKey(fileKey);
            var request = new InitiateMultipartUploadRequest
                              {
                                  BucketName = _filesBucketName,
                                  Key = key,
                                  ContentType = contentType
                              };
            var metadataWrapper = MetadataCollectionWrapper.For(request.Metadata);
            metadataWrapper.Write(MetadataElement.Filename, fileName);

            var uploadResponse = await _cephS3Client.InitiateMultipartUploadAsync(request);

            return new MultipartUploadSession(sessionId, sessionDescriptor, expiresAt, elementDescriptor, fileKey, fileName, uploadResponse.UploadId);
        }

        public async Task UploadFilePart(MultipartUploadSession uploadSession, Stream inputStream, int templateCode)
        {
            if (SessionDescriptor.IsSessionExpired(uploadSession.SessionExpiresAt))
            {
                throw new SessionExpiredException(uploadSession.SessionId, uploadSession.SessionExpiresAt);
            }

            if (uploadSession.NextPartNumber == 1)
            {
                EnsureFileHeaderIsValid(
                    templateCode,
                    uploadSession.FileName,
                    uploadSession.ElementDescriptor.Type,
                    inputStream);
            }

            var key = uploadSession.SessionId.AsS3ObjectKey(uploadSession.FileKey);
            var response = await _cephS3Client.UploadPartAsync(
                                new UploadPartRequest
                                    {
                                        BucketName = _filesBucketName,
                                        Key = key,
                                        UploadId = uploadSession.UploadId,
                                        InputStream = inputStream,
                                        PartNumber = uploadSession.NextPartNumber
                                    });
            uploadSession.AddPart(response.ETag);
        }

        public async Task AbortMultipartUpload(MultipartUploadSession uploadSession)
        {
            if (!uploadSession.IsCompleted)
            {
                var key = uploadSession.SessionId.AsS3ObjectKey(uploadSession.FileKey);
                await _cephS3Client.AbortMultipartUploadAsync(_filesBucketName, key, uploadSession.UploadId);
            }
        }

        public async Task<UploadedFileInfo> CompleteMultipartUpload(MultipartUploadSession uploadSession, int templateCode, IFileUploadParams fileUploadParams)
        {
            var uploadKey = uploadSession.SessionId.AsS3ObjectKey(uploadSession.FileKey);
            var partETags = uploadSession.Parts.Select(x => new PartETag(x.PartNumber, x.Etag)).ToList();
            var uploadResponse = await _cephS3Client.CompleteMultipartUploadAsync(
                                     new CompleteMultipartUploadRequest
                                         {
                                             BucketName = _filesBucketName,
                                             Key = uploadKey,
                                             UploadId = uploadSession.UploadId,
                                             PartETags = partETags
                                         });
            uploadSession.Complete();

            if (SessionDescriptor.IsSessionExpired(uploadSession.SessionExpiresAt))
            {
                throw new SessionExpiredException(uploadSession.SessionId, uploadSession.SessionExpiresAt);
            }

            try
            {
                using (var getResponse = await _cephS3Client.GetObjectAsync(_filesBucketName, uploadKey))
                {
                    string contentType;
                    using (getResponse.ResponseStream)
                    {
                        var sessionDescriptor = uploadSession.SessionDescriptor;
                        var elementDescriptor = uploadSession.ElementDescriptor;
                        contentType = EnsureFileContentIsValid(
                            elementDescriptor.TemplateCode,
                            uploadSession.FileName,
                            elementDescriptor.Type,
                            elementDescriptor.Constraints.For(sessionDescriptor.Language),
                            getResponse.ResponseStream,
                            fileUploadParams);
                    }

                    var metadataWrapper = MetadataCollectionWrapper.For(getResponse.Metadata);
                    var fileName = metadataWrapper.Read<string>(MetadataElement.Filename);

                    var fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
                    var fileKey = Path.ChangeExtension(uploadSession.SessionId.AsS3ObjectKey(uploadResponse.ETag), fileExtension);
                    var copyRequest = new CopyObjectRequest
                        {
                            ContentType = contentType,
                            SourceBucket = _filesBucketName,
                            SourceKey = uploadKey,
                            DestinationBucket = _filesBucketName,
                            DestinationKey = fileKey,
                            MetadataDirective = S3MetadataDirective.REPLACE,
                            CannedACL = S3CannedACL.PublicRead
                        };
                    foreach (var metadataKey in getResponse.Metadata.Keys)
                    {
                        copyRequest.Metadata.Add(metadataKey, getResponse.Metadata[metadataKey]);
                    }

                    await _cephS3Client.CopyObjectAsync(copyRequest);
                    _uploadedBinariesMetric.Inc();

                    _memoryCache.Set(fileKey, new BinaryMetadata(fileName, getResponse.ContentLength), uploadSession.SessionExpiresAt);

                    return new UploadedFileInfo(fileKey, new Uri(_fileStorageEndpointUri, fileKey));
                }
            }
            finally
            {
                await _cephS3Client.DeleteObjectAsync(_filesBucketName, uploadKey);
            }
        }

        private static void EnsureFileMetadataIsValid(IElementDescriptor elementDescriptor, long inputStreamLength, Language language, string fileName)
        {
            var constraints = (IBinaryElementConstraints)elementDescriptor.Constraints.For(language);
            if (constraints.MaxFilenameLength < fileName.Length)
            {
                throw new InvalidBinaryException(elementDescriptor.TemplateCode, new FilenameTooLongError(fileName.Length));
            }

            if (constraints.MaxSize < inputStreamLength)
            {
                throw new InvalidBinaryException(elementDescriptor.TemplateCode, new BinaryTooLargeError(inputStreamLength));
            }

            var extension = GetDotLessExtension(fileName);
            if (!ValidateFileExtension(extension, constraints))
            {
                throw new InvalidBinaryException(elementDescriptor.TemplateCode, new BinaryInvalidFormatError(extension));
            }
        }

        private static void EnsureFileHeaderIsValid(
            int templateCode,
            string fileName,
            ElementDescriptorType elementDescriptorType,
            Stream inputStream)
        {
            var fileFormat = DetectFileFormat(fileName);
            switch (elementDescriptorType)
            {
                case ElementDescriptorType.BitmapImage:
                case ElementDescriptorType.Logo:
                    BitmapImageValidator.ValidateBitmapImageHeader(templateCode, fileFormat, inputStream);
                    break;
                case ElementDescriptorType.VectorImage:
                    VectorImageValidator.ValidateVectorImageHeader(templateCode, fileFormat, inputStream);
                    break;
                case ElementDescriptorType.Article:
                    break;
                case ElementDescriptorType.PlainText:
                case ElementDescriptorType.FormattedText:
                case ElementDescriptorType.FasComment:
                case ElementDescriptorType.Link:
                case ElementDescriptorType.Phone:
                case ElementDescriptorType.VideoLink:
                case ElementDescriptorType.Color:
                    throw new NotSupportedException($"Not binary element descriptor type {elementDescriptorType}");
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementDescriptorType), elementDescriptorType, "Unsupported element descriptor type");
            }
        }

        private static string EnsureFileContentIsValid(int templateCode,
                                                       string fileName,
                                                       ElementDescriptorType elementDescriptorType,
                                                       IElementConstraints elementConstraints,
                                                       Stream inputStream,
                                                       IFileUploadParams fileUploadParams)
        {
            switch (elementDescriptorType)
            {
                case ElementDescriptorType.BitmapImage:
                    return BitmapImageValidator.ValidateBitmapImage(templateCode, (BitmapImageElementConstraints)elementConstraints, inputStream);
                case ElementDescriptorType.VectorImage:
                {
                    var fileFormat = DetectFileFormat(fileName);
                    VectorImageValidator.ValidateVectorImage(templateCode, fileFormat, (VectorImageElementConstraints)elementConstraints, inputStream);
                    return ContentTypesMap[fileFormat];
                }
                case ElementDescriptorType.Article:
                    ArticleValidator.ValidateArticle(templateCode, inputStream);
                    return ContentTypesMap[FileFormat.Chm];
                case ElementDescriptorType.Logo:
                    return fileUploadParams is CustomImageFileUploadParams customImageFileUploadParams
                               ? LogoImageValidator.ValidateLogoCustomImage(templateCode, (LogoElementConstraints)elementConstraints, inputStream, customImageFileUploadParams)
                               : LogoImageValidator.ValidateLogoOriginal(templateCode, (LogoElementConstraints)elementConstraints, inputStream);
                case ElementDescriptorType.PlainText:
                case ElementDescriptorType.FormattedText:
                case ElementDescriptorType.FasComment:
                case ElementDescriptorType.Link:
                case ElementDescriptorType.Phone:
                case ElementDescriptorType.VideoLink:
                case ElementDescriptorType.Color:
                    throw new NotSupportedException($"Not binary element descriptor type {elementDescriptorType}");
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementDescriptorType), elementDescriptorType, "Unsupported element descriptor type");
            }
        }

        private static FileFormat DetectFileFormat(string fileName)
        {
            var extension = GetDotLessExtension(fileName);
            if (Enum.TryParse(extension, true, out FileFormat format))
            {
                return format;
            }

            throw new ArgumentException($"Filename '{fileName}' does not have appropriate extension", nameof(fileName));
        }

        private static bool ValidateFileExtension(string extension, IBinaryElementConstraints constraints)
        {
            return Enum.TryParse(extension, true, out FileFormat format)
                   && Enum.IsDefined(typeof(FileFormat), format)
                   && format.ToString().Equals(extension, StringComparison.OrdinalIgnoreCase)
                   && constraints.SupportedFileFormats.Any(f => f == format);
        }

        private static string GetDotLessExtension(string path)
        {
            var dottedExtension = Path.GetExtension(path);
            return string.IsNullOrEmpty(dottedExtension)
                       ? dottedExtension
                       : dottedExtension.Trim('.').ToLowerInvariant();
        }

        private async Task<IElementDescriptor> GetElementDescriptor(long templateId, string templateVersionId, int templateCode)
        {
            var templateDescriptor = await _templatesStorageReader.GetTemplateDescriptor(templateId, templateVersionId);
            return templateDescriptor.Elements.Single(x => x.TemplateCode == templateCode);
        }
    }
}
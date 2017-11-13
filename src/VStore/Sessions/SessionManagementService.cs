﻿using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

using Amazon.S3;
using Amazon.S3.Model;

using CHMsharp;

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
using NuClear.VStore.Sessions.ContentValidation.Errors;
using NuClear.VStore.Templates;

using Prometheus.Client;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionManagementService
    {
        private const string PdfHeader = "%PDF-";

        private static readonly IReadOnlyDictionary<FileFormat, string> BitmapImageFormatsMap =
            new Dictionary<FileFormat, string>
                {
                        { FileFormat.Bmp, "image/bmp" },
                        { FileFormat.Gif, "image/gif" },
                        { FileFormat.Jpeg, "image/jpeg" },
                        { FileFormat.Jpg, "image/jpeg" },
                        { FileFormat.Png, "image/png" }
                };

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

        public async Task<UploadedFileInfo> CompleteMultipartUpload(MultipartUploadSession uploadSession, int templateCode)
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
                            getResponse.ResponseStream);
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
                    ValidateBitmapImageHeader(templateCode, fileFormat, inputStream);
                    break;
                case ElementDescriptorType.VectorImage:
                    ValidateVectorImageHeader(templateCode, fileFormat, inputStream);
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

        private static string EnsureFileContentIsValid(
            int templateCode,
            string fileName,
            ElementDescriptorType elementDescriptorType,
            IElementConstraints elementDescriptorConstraints,
            Stream inputStream)
        {
            switch (elementDescriptorType)
            {
                case ElementDescriptorType.BitmapImage:
                    return ValidateBitmapImage(templateCode, (BitmapImageElementConstraints)elementDescriptorConstraints, inputStream);
                case ElementDescriptorType.VectorImage:
                    return ValidateVectorImage(templateCode, DetectFileFormat(fileName), inputStream);
                case ElementDescriptorType.Article:
                    return ValidateArticle(templateCode, inputStream);
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

        private static void ValidateVectorImageHeader(int templateCode, FileFormat fileFormat, Stream inputStream)
        {
            switch (fileFormat)
            {
                case FileFormat.Svg:
                    break;
                case FileFormat.Pdf:
                    ValidatePdf(templateCode, inputStream);
                    break;
                case FileFormat.Png:
                case FileFormat.Gif:
                case FileFormat.Bmp:
                case FileFormat.Chm:
                case FileFormat.Jpg:
                case FileFormat.Jpeg:
                    throw new NotSupportedException($"Not vector image file format {fileFormat}");
                default:
                    throw new ArgumentOutOfRangeException(nameof(fileFormat), fileFormat, "Unsupported file format");
            }
        }

        private static string ValidateVectorImage(int templateCode, FileFormat fileFormat, Stream inputStream)
        {
            switch (fileFormat)
            {
                case FileFormat.Svg:
                    ValidateSvg(templateCode, inputStream);
                    break;
                case FileFormat.Pdf:
                    break;
                case FileFormat.Png:
                case FileFormat.Gif:
                case FileFormat.Bmp:
                case FileFormat.Chm:
                case FileFormat.Jpg:
                case FileFormat.Jpeg:
                    throw new NotSupportedException($"Not vector image file format {fileFormat}");
                default:
                    throw new ArgumentOutOfRangeException(nameof(fileFormat), fileFormat, "Unsupported file format");
            }

            return ContentTypesMap[fileFormat];
        }

        private static void ValidateBitmapImageHeader(int templateCode, FileFormat fileFormat, Stream inputStream)
        {
            var format = Image.DetectFormat(inputStream);
            if (format == null)
            {
                throw new InvalidBinaryException(templateCode, new InvalidImageError());
            }

            var extension = fileFormat.ToString().ToLowerInvariant();
            // Image format is not consistent with filename extension:
            if (!format.FileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidBinaryException(templateCode, new BinaryExtensionMismatchContentError(extension, format.Name.ToLowerInvariant()));
            }
        }

        private static string ValidateBitmapImage(int templateCode, BitmapImageElementConstraints constraints, Stream inputStream)
        {
            var imageFormats = constraints.SupportedFileFormats
                                          .Aggregate(
                                              new List<string>(),
                                              (result, next) =>
                                                  {
                                                      if (BitmapImageFormatsMap.TryGetValue(next, out var imageFormat))
                                                      {
                                                          result.Add(imageFormat);
                                                      }

                                                      return result;
                                                  });

            Image<Rgba32> image;
            IImageFormat format;
            try
            {
                image = Image.Load(inputStream, out format);
            }
            catch (Exception)
            {
                throw new InvalidBinaryException(templateCode, new InvalidImageError());
            }

            using (image)
            {
                if (!imageFormats.Contains(format.DefaultMimeType, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidBinaryException(templateCode, new BinaryInvalidFormatError(format.Name.ToLowerInvariant()));
                }

                if (constraints.SupportedImageSizes.All(x => image.Width != x.Width || image.Height != x.Height))
                {
                    throw new InvalidBinaryException(templateCode, new ImageUnsupportedSizeError(new ImageSize { Height = image.Height, Width = image.Width }));
                }

                if (constraints.IsAlphaChannelRequired && !IsImageContainsAlphaChannel(image))
                {
                    throw new InvalidBinaryException(templateCode, new ImageAlphaChannelError());
                }
            }

            return format.DefaultMimeType;
        }

        private static void ValidatePdf(int templateCode, Stream inputStream)
        {
            var position = inputStream.Position;
            inputStream.Seek(0, SeekOrigin.Begin);
            var br = new BinaryReader(inputStream, Encoding.ASCII);
            var header = new string(br.ReadChars(PdfHeader.Length));
            inputStream.Position = position;

            if (!header.StartsWith(PdfHeader))
            {
                throw new InvalidBinaryException(templateCode, new InvalidImageError());
            }
        }

        private static void ValidateSvg(int templateCode, Stream inputStream)
        {
            XDocument xdoc;
            try
            {
                xdoc = XDocument.Load(inputStream);
            }
            catch (XmlException)
            {
                throw new InvalidBinaryException(templateCode, new InvalidImageError());
            }

            var svgFormat = FileFormat.Svg.ToString();
            if (xdoc.Root == null || !svgFormat.Equals(xdoc.Root.Name.LocalName, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidBinaryException(templateCode, new InvalidImageError());
            }
        }

        private static bool IsImageContainsAlphaChannel(Image<Rgba32> image)
        {
            for (var x = 0; x < image.Width; ++x)
            {
                for (var y = 0; y < image.Height; ++y)
                {
                    if (image[x, y].A != byte.MaxValue)
                    {
                        return true;
                    }
                }
            }

            return false;
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

        private static string ValidateArticle(int templateCode, Stream inputStream)
        {
            var enumeratorContext = new EnumeratorContext { IsGoalReached = false };
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    inputStream.CopyTo(memoryStream);
                    memoryStream.Position = 0;

                    ChmFile.Open(memoryStream)
                           .Enumerate(
                               EnumerateLevel.Normal | EnumerateLevel.Files,
                               ArticleEnumeratorCallback,
                               enumeratorContext);
                }
            }
            catch (InvalidDataException)
            {
                throw new InvalidBinaryException(templateCode, new InvalidArticleError());
            }

            if (!enumeratorContext.IsGoalReached)
            {
                throw new InvalidBinaryException(templateCode, new ArticleMissingIndexError());
            }

            return ContentTypesMap[FileFormat.Chm];
        }

        private static EnumerateStatus ArticleEnumeratorCallback(ChmFile file, ChmUnitInfo unitInfo, EnumeratorContext context)
        {
            if (string.Equals(unitInfo.path.TrimStart('/'), "index.html", StringComparison.OrdinalIgnoreCase))
            {
                context.IsGoalReached = true;
                return EnumerateStatus.Success;
            }

            return EnumerateStatus.Continue;
        }

        private async Task<IElementDescriptor> GetElementDescriptor(long templateId, string templateVersionId, int templateCode)
        {
            var templateDescriptor = await _templatesStorageReader.GetTemplateDescriptor(templateId, templateVersionId);
            return templateDescriptor.Elements.Single(x => x.TemplateCode == templateCode);
        }
    }
}
using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using CHMsharp;

using ImageSharp;
using ImageSharp.Formats;

using Newtonsoft.Json;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Sessions;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Events;
using NuClear.VStore.Http;
using NuClear.VStore.Json;
using NuClear.VStore.Kafka;
using NuClear.VStore.Options;
using NuClear.VStore.S3;
using NuClear.VStore.Sessions.ContentValidation.Errors;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionManagementService
    {
        private static readonly Dictionary<FileFormat, IImageFormat> ImageFormatsMap =
            new Dictionary<FileFormat, IImageFormat>
                {
                        { FileFormat.Bmp, new BmpFormat() },
                        { FileFormat.Gif, new GifFormat() },
                        { FileFormat.Jpeg, new JpegFormat() },
                        { FileFormat.Jpg, new JpegFormat() },
                        { FileFormat.Png, new PngFormat() }
                };

        private readonly TimeSpan _sessionExpiration;
        private readonly Uri _fileStorageEndpointUri;
        private readonly string _filesBucketName;
        private readonly string _sessionsTopicName;
        private readonly IAmazonS3 _amazonS3;
        private readonly SessionStorageReader _sessionStorageReader;
        private readonly TemplatesStorageReader _templatesStorageReader;
        private readonly EventSender _eventSender;

        public SessionManagementService(
            CephOptions cephOptions,
            VStoreOptions vstoreOptions,
            KafkaOptions kafkaOptions,
            IAmazonS3 amazonS3,
            SessionStorageReader sessionStorageReader,
            TemplatesStorageReader templatesStorageReader,
            EventSender eventSender)
        {
            _sessionExpiration = vstoreOptions.SessionExpiration;
            _fileStorageEndpointUri = vstoreOptions.FileStorageEndpoint;
            _filesBucketName = cephOptions.FilesBucketName;
            _sessionsTopicName = kafkaOptions.SessionsTopic;
            _amazonS3 = amazonS3;
            _sessionStorageReader = sessionStorageReader;
            _templatesStorageReader = templatesStorageReader;
            _eventSender = eventSender;
        }

        public async Task<SessionContext> GetSessionContext(Guid sessionId)
        {
            (var sessionDescriptor, var authorInfo, var expiresAt) = await _sessionStorageReader.GetSessionDescriptor(sessionId);

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

            await _amazonS3.PutObjectAsync(request);
        }

        public async Task<MultipartUploadSession> InitiateMultipartUpload(
            Guid sessionId,
            string fileName,
            string contentType,
            int templateCode)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                throw new MissingFilenameException($"Filename has not been provided for the item '{templateCode}'");
            }

            (var sessionDescriptor, var _, var _) = await _sessionStorageReader.GetSessionDescriptor(sessionId);
            if (sessionDescriptor.BinaryElementTemplateCodes.All(x => x != templateCode))
            {
                throw new InvalidTemplateException(
                          $"Binary content is not expected for the item '{templateCode}' within template '{sessionDescriptor.TemplateId}' " +
                          $"with version Id '{sessionDescriptor.TemplateVersionId}'.");
            }

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

            var uploadResponse = await _amazonS3.InitiateMultipartUploadAsync(request);

            return new MultipartUploadSession(sessionId, fileKey, uploadResponse.UploadId);
        }

        public async Task UploadFilePart(MultipartUploadSession uploadSession, Stream inputStream, string fileName, int templateCode)
        {
            using (var memory = new MemoryStream())
            {
                await inputStream.CopyToAsync(memory);
                memory.Position = 0;

                if (uploadSession.NextPartNumber == 1)
                {
                    (var sessionDescriptor, var _, var _) = await _sessionStorageReader.GetSessionDescriptor(uploadSession.SessionId);
                    var elementDescriptor = await GetElementDescriptor(sessionDescriptor.TemplateId, sessionDescriptor.TemplateVersionId, templateCode);
                    EnsureFileIsValid(elementDescriptor, memory, sessionDescriptor.Language, fileName);
                }

                var key = uploadSession.SessionId.AsS3ObjectKey(uploadSession.FileKey);
                var response = await _amazonS3.UploadPartAsync(
                                   new UploadPartRequest
                                       {
                                           BucketName = _filesBucketName,
                                           Key = key,
                                           UploadId = uploadSession.UploadId,
                                           InputStream = memory,
                                           PartNumber = uploadSession.NextPartNumber
                                       });
                uploadSession.AddPart(response.ETag);
            }
        }

        public async Task AbortMultipartUpload(MultipartUploadSession uploadSession)
        {
            if (!uploadSession.IsCompleted)
            {
                var key = uploadSession.SessionId.AsS3ObjectKey(uploadSession.FileKey);
                await _amazonS3.AbortMultipartUploadAsync(_filesBucketName, key, uploadSession.UploadId);
            }
        }

        public async Task<UploadedFileInfo> CompleteMultipartUpload(MultipartUploadSession uploadSession, int templateCode)
        {
            var uploadKey = uploadSession.SessionId.AsS3ObjectKey(uploadSession.FileKey);
            var partETags = uploadSession.Parts.Select(x => new PartETag(x.PartNumber, x.Etag)).ToList();
            var uploadResponse = await _amazonS3.CompleteMultipartUploadAsync(
                                     new CompleteMultipartUploadRequest
                                         {
                                             BucketName = _filesBucketName,
                                             Key = uploadKey,
                                             UploadId = uploadSession.UploadId,
                                             PartETags = partETags
                                         });
            uploadSession.Complete();

            (var sessionDescriptor, var _, var _) = await _sessionStorageReader.GetSessionDescriptor(uploadSession.SessionId);
            var elementDescriptor = await GetElementDescriptor(sessionDescriptor.TemplateId, sessionDescriptor.TemplateVersionId, templateCode);
            try
            {
                var getResponse = await _amazonS3.GetObjectAsync(_filesBucketName, uploadKey);
                using (getResponse.ResponseStream)
                {
                    EnsureUploadedFileIsValid(
                        elementDescriptor.TemplateCode,
                        elementDescriptor.Type,
                        elementDescriptor.Constraints.For(sessionDescriptor.Language),
                        getResponse.ResponseStream,
                        getResponse.ContentLength);
                }

                var metadataWrapper = MetadataCollectionWrapper.For(getResponse.Metadata);
                var fileName = metadataWrapper.Read<string>(MetadataElement.Filename);

                var fileExtension = Path.GetExtension(fileName);
                var fileKey = Path.ChangeExtension(uploadSession.SessionId.AsS3ObjectKey(uploadResponse.ETag), fileExtension);
                var copyRequest = new CopyObjectRequest
                                      {
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

                await _amazonS3.CopyObjectAsync(copyRequest);

                return new UploadedFileInfo(fileKey, new Uri(_fileStorageEndpointUri, fileKey));
            }
            finally
            {
                await _amazonS3.DeleteObjectAsync(_filesBucketName, uploadKey);
            }
        }

        private static void EnsureFileIsValid(IElementDescriptor elementDescriptor, Stream inputStream, Language language, string fileName)
        {
            var constraints = (IBinaryElementConstraints)elementDescriptor.Constraints.For(language);
            if (constraints.MaxFilenameLength.HasValue && constraints.MaxFilenameLength.Value < fileName.Length)
            {
                throw new InvalidBinaryException(elementDescriptor.TemplateCode, new FilenameTooLongError(fileName.Length));
            }

            if (constraints.MaxSize.HasValue && constraints.MaxSize.Value < inputStream.Length)
            {
                throw new InvalidBinaryException(elementDescriptor.TemplateCode, new BinaryTooLargeError(inputStream.Length));
            }

            var extension = GetDotLessExtension(fileName);
            if (!ValidateFileExtension(extension, constraints))
            {
                throw new InvalidBinaryException(elementDescriptor.TemplateCode, new BinaryInvalidFormatError(extension));
            }

            if (elementDescriptor.Type == ElementDescriptorType.Image)
            {
                var maxHeaderSize = ImageFormatsMap.Values.Max(x => x.HeaderSize);
                var header = new byte[maxHeaderSize];

                var position = inputStream.Position;
                inputStream.Read(header, 0, header.Length);
                inputStream.Position = position;

                var format = ImageFormatsMap.Values.FirstOrDefault(x => x.IsSupportedFileFormat(header.Take(x.HeaderSize).ToArray()));
                if (format == null)
                {
                    throw new InvalidBinaryException(elementDescriptor.TemplateCode, new InvalidImageError());
                }

                // Image format is not consistent with filename extension:
                if (!format.SupportedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidBinaryException(elementDescriptor.TemplateCode, new BinaryExtensionMismatchContentError(extension, format.Extension.ToLowerInvariant()));
                }
            }
        }

        private static void EnsureUploadedFileIsValid(
            int templateCode,
            ElementDescriptorType elementDescriptorType,
            IElementConstraints elementDescriptorConstraints,
            Stream inputStream,
            long inputStreamLength)
        {
            switch (elementDescriptorType)
            {
                case ElementDescriptorType.Image:
                    ValidateImage(templateCode, (ImageElementConstraints)elementDescriptorConstraints, inputStream, inputStreamLength);
                    break;
                case ElementDescriptorType.Article:
                    ValidateArticle(templateCode, (ArticleElementConstraints)elementDescriptorConstraints, inputStream, inputStreamLength);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementDescriptorType));
            }
        }

        private static void ValidateImage(int templateCode, ImageElementConstraints constraints, Stream inputStream, long inputStreamLength)
        {
            if (inputStreamLength > constraints.MaxSize)
            {
                throw new InvalidBinaryException(templateCode, new BinaryTooLargeError(inputStreamLength));
            }

            var imageFormats = constraints.SupportedFileFormats
                                          .Aggregate(
                                              new List<IImageFormat>(),
                                              (result, next) =>
                                                  {
                                                      if (ImageFormatsMap.TryGetValue(next, out IImageFormat imageFormat))
                                                      {
                                                          result.Add(imageFormat);
                                                      }

                                                      return result;
                                                  });

            Image<Rgba32> image;
            try
            {
                image = Image.Load(inputStream);
            }
            catch (Exception)
            {
                throw new InvalidBinaryException(templateCode, new InvalidImageError());
            }

            using (image)
            {
                if (!imageFormats.Exists(x => x.GetType() == image.CurrentImageFormat.GetType()))
                {
                    throw new InvalidBinaryException(templateCode, new BinaryInvalidFormatError(image.CurrentImageFormat.Extension));
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
        }

        private static bool IsImageContainsAlphaChannel(IImageBase<Rgba32> image)
        {
            var pixels = image.Pixels;
            for (var i = 0; i < pixels.Length; ++i)
            {
                if (pixels[i].A != byte.MaxValue)
                {
                    return true;
                }
            }

            return false;
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

        private static void ValidateArticle(int templateCode, ArticleElementConstraints constraints, Stream inputStream, long inputStreamLength)
        {
            if (inputStreamLength > constraints.MaxSize)
            {
                throw new InvalidBinaryException(templateCode, new BinaryTooLargeError(inputStreamLength));
            }

            var enumeratorContext = new EnumeratorContext { IsGoalReached = false };
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    inputStream.CopyTo(memoryStream);

                    ChmFile.Open(memoryStream)
                           .Enumerate(
                               EnumerateLevel.Normal | EnumerateLevel.Files,
                               ArticleEnumeratorCallback,
                               enumeratorContext);
                }
            }
            catch (Exception)
            {
                throw new InvalidBinaryException(templateCode, new InvalidArticleError());
            }

            if (!enumeratorContext.IsGoalReached)
            {
                throw new InvalidBinaryException(templateCode, new ArticleMissingIndexError());
            }
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
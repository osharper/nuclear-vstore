using System;
using System.Collections.Generic;

using System.IO;
using System.Linq;
using System.Text;
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
using NuClear.VStore.Json;
using NuClear.VStore.S3;
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

        private readonly Uri _fileStorageEndpointUri;
        private readonly string _filesBucketName;
        private readonly IAmazonS3 _amazonS3;
        private readonly TemplateStorageReader _templateStorageReader;

        public SessionManagementService(
            Uri fileStorageEndpointUri,
            string filesBucketName,
            IAmazonS3 amazonS3,
            TemplateStorageReader templateStorageReader)
        {
            _fileStorageEndpointUri = fileStorageEndpointUri;
            _filesBucketName = filesBucketName;
            _amazonS3 = amazonS3;
            _templateStorageReader = templateStorageReader;
        }

        public async Task<SessionSetupContext> Setup(long templateId, Language language)
        {
            if (language == Language.Unspecified)
            {
                throw new SessionCannotBeCreatedException("Language must be explicitly specified.");
            }

            var templateDescriptor = await _templateStorageReader.GetTemplateDescriptor(templateId, null);

            var setupContext = new SessionSetupContext(templateDescriptor);
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
                                  Key = setupContext.Id.AsS3ObjectKey(Tokens.SessionPostfix),
                                  CannedACL = S3CannedACL.PublicRead,
                                  ContentType = ContentType.Json,
                                  ContentBody = JsonConvert.SerializeObject(sessionDescriptor, SerializerSettings.Default)
                              };
            var metadataWrapper = MetadataCollectionWrapper.For(request.Metadata);
            metadataWrapper.Write(MetadataElement.ExpiresAt, setupContext.ExpiresAt);

            await _amazonS3.PutObjectAsync(request);

            return setupContext;
        }

        public async Task<MultipartUploadSession> InitiateMultipartUpload(
            Guid sessionId,
            string fileName,
            string contentType,
            long contentLength,
            int templateCode)
        {
            if (!await IsSessionExists(sessionId))
            {
                throw new InvalidOperationException($"Session '{sessionId}' does not exist");
            }

            var sessionDescriptor = await GetSessionDescriptor(sessionId);
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

            return new MultipartUploadSession(sessionId, fileName, fileKey, uploadResponse.UploadId);
        }

        public async Task UploadFilePart(MultipartUploadSession uploadSession, Stream inputStream, int templateCode)
        {
            using (var memory = new MemoryStream())
            {
                await inputStream.CopyToAsync(memory);
                memory.Position = 0;

                if (uploadSession.NextPartNumber == 1)
                {
                    var sessionDescriptor = await GetSessionDescriptor(uploadSession.SessionId);
                    var elementDescriptor = await GetElementDescriptor(sessionDescriptor.TemplateId, sessionDescriptor.TemplateVersionId, templateCode);
                    EnsureFileHeaderIsValid(elementDescriptor, memory);
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

            var sessionDescriptor = await GetSessionDescriptor(uploadSession.SessionId);
            var elementDescriptor = await GetElementDescriptor(sessionDescriptor.TemplateId, sessionDescriptor.TemplateVersionId, templateCode);
            try
            {
                var getResponse = await _amazonS3.GetObjectAsync(_filesBucketName, uploadKey);
                using (getResponse.ResponseStream)
                {
                    EnsureUploadedFileIsValid(
                        elementDescriptor.Type,
                        elementDescriptor.Constraints.For(sessionDescriptor.Language),
                        getResponse.ResponseStream,
                        getResponse.ContentLength);
                }

                var fileKey = uploadSession.SessionId.AsS3ObjectKey(uploadResponse.ETag);
                var copyRequest = new CopyObjectRequest
                                      {
                                          SourceBucket = _filesBucketName,
                                          SourceKey = uploadKey,
                                          DestinationBucket = _filesBucketName,
                                          DestinationKey = fileKey,
                                          CannedACL = S3CannedACL.PublicRead
                                      };
                await _amazonS3.CopyObjectAsync(copyRequest);

                return new UploadedFileInfo(fileKey, uploadSession.FileName, new Uri(_fileStorageEndpointUri, fileKey));
            }
            finally
            {
                await _amazonS3.DeleteObjectAsync(_filesBucketName, uploadKey);
            }
        }

        private static void EnsureFileHeaderIsValid(IElementDescriptor elementDescriptor, Stream inputStream)
        {
            if (elementDescriptor.Type == ElementDescriptorType.Image)
            {
                var maxHeaderSize = ImageFormatsMap.Values.Max(x => x.HeaderSize);
                var header = new byte[maxHeaderSize];

                var position = inputStream.Position;
                inputStream.Read(header, 0, header.Length);
                inputStream.Position = position;

                if (!ImageFormatsMap.Values.Any(x => x.IsSupportedFileFormat(header.Take(x.HeaderSize).ToArray())))
                {
                    throw new ImageIncorrectException("Input stream cannot be recognized as image.");
                }
            }
        }

        private static void EnsureUploadedFileIsValid(
            ElementDescriptorType elementDescriptorType,
            IElementConstraints elementDescriptorConstraints,
            Stream inputStream,
            long inputStreamLength)
        {
            switch (elementDescriptorType)
            {
                case ElementDescriptorType.Image:
                    ValidateImage((ImageElementConstraints)elementDescriptorConstraints, inputStream, inputStreamLength);
                    break;
                case ElementDescriptorType.Article:
                    ValidateArticle((ArticleElementConstraints)elementDescriptorConstraints, inputStream, inputStreamLength);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementDescriptorType));
            }
        }

        private static void ValidateImage(ImageElementConstraints constraints, Stream inputStream, long inputStreamLength)
        {
            if (inputStreamLength > constraints.MaxSize)
            {
                throw new FilesizeMismatchException("Image exceeds the size limit.");
            }

            Image image;
            try
            {
                image = new Image(inputStream);
            }
            catch (Exception ex)
            {
                throw new ImageIncorrectException("Image cannot be loaded from the stream.", ex);
            }

            var imageFormats = constraints.SupportedFileFormats
                                               .Aggregate(
                                                   new List<IImageFormat>(),
                                                   (result, next) =>
                                                       {
                                                           IImageFormat imageFormat;
                                                           if (ImageFormatsMap.TryGetValue(next, out imageFormat))
                                                           {
                                                               result.Add(imageFormat);
                                                           }

                                                           return result;
                                                       });

            if (!imageFormats.Exists(x => x.GetType() == image.CurrentImageFormat.GetType()))
            {
                throw new ImageIncorrectException("Image has an incorrect format");
            }

            if (image.Width != constraints.ImageSize.Width || image.Height != constraints.ImageSize.Height)
            {
                throw new ImageIncorrectException("Image has an incorrect size");
            }
        }

        private static void ValidateArticle(ArticleElementConstraints constraints, Stream inputStream, long inputStreamLength)
        {
            if (inputStreamLength > constraints.MaxSize)
            {
                throw new FilesizeMismatchException("Article exceeds the size limit");
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
            catch (Exception ex)
            {
                throw new ArticleIncorrectException("Article cannot be loaded from the stream", ex);
            }

            if (!enumeratorContext.IsGoalReached)
            {
                throw new ArticleIncorrectException("Article must contain 'index.html'");
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

        private async Task<bool> IsSessionExists(Guid sessionId)
        {
            var response = await _amazonS3.ListObjectsAsync(
                               new ListObjectsRequest
                                   {
                                       BucketName = _filesBucketName,
                                       Prefix = sessionId.ToString()
                                   });
            return response.S3Objects.Count > 0;
        }

        private async Task<SessionDescriptor> GetSessionDescriptor(Guid sessionId)
        {
            var objectResponse = await _amazonS3.GetObjectAsync(_filesBucketName, sessionId.AsS3ObjectKey(Tokens.SessionPostfix));
            var metadataWrapper = MetadataCollectionWrapper.For(objectResponse.Metadata);

            var expiresAt = metadataWrapper.Read<DateTime>(MetadataElement.ExpiresAt);
            if (SessionSetupContext.IsSessionExpired(expiresAt))
            {
                throw new SessionExpiredException(sessionId);
            }

            string json;
            using (var reader = new StreamReader(objectResponse.ResponseStream, Encoding.UTF8))
            {
                json = reader.ReadToEnd();
            }

            return JsonConvert.DeserializeObject<SessionDescriptor>(json, SerializerSettings.Default);
        }

        private async Task<IElementDescriptor> GetElementDescriptor(long templateId, string templateVersionId, int templateCode)
        {
            var templateDescriptor = await _templateStorageReader.GetTemplateDescriptor(templateId, templateVersionId);
            return templateDescriptor.Elements.Single(x => x.TemplateCode == templateCode);
        }
    }
}
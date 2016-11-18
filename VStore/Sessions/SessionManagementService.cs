using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using ImageSharp;

using NuClear.VStore.Descriptors.Sessions;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionManagementService
    {
        private const string SessionToken = "session";

        private readonly Uri _endpointUri;
        private readonly Uri _fileStorageEndpointUri;
        private readonly string _filesBucketName;
        private readonly IAmazonS3 _amazonS3;
        private readonly TemplateStorageReader _templateStorageReader;

        public SessionManagementService(
            Uri endpointUri,
            Uri fileStorageEndpointUri,
            string filesBucketName,
            IAmazonS3 amazonS3,
            TemplateStorageReader templateStorageReader)
        {
            _endpointUri = endpointUri;
            _fileStorageEndpointUri = fileStorageEndpointUri;
            _filesBucketName = filesBucketName;
            _amazonS3 = amazonS3;
            _templateStorageReader = templateStorageReader;
        }

        public async Task<SessionDescriptor> Setup(long templateId)
        {
            var templateDescriptor = await _templateStorageReader.GetTemplateDescriptor(templateId, null);
            var sessionDescriptor = new SessionDescriptor(_endpointUri, templateDescriptor);

            if (sessionDescriptor.UploadUris.Count == 0)
            {
                throw new SessionCannotBeCreatedException(
                          $"There is no binary content can be uploaded for template '{templateDescriptor.Id}' " +
                          $"with version '{templateDescriptor.VersionId}'");
            }

            var request = new PutObjectRequest
                              {
                                  BucketName = _filesBucketName,
                                  Key = $"{sessionDescriptor.Id}/{SessionToken}",
                                  CannedACL = S3CannedACL.PublicRead
                              };
            var metadataWrapper = MetadataCollectionWrapper.For(request.Metadata);
            metadataWrapper.Write(MetadataElement.TemplateId, templateDescriptor.Id);
            metadataWrapper.Write(MetadataElement.TemplateVersionId, templateDescriptor.VersionId);
            metadataWrapper.Write(MetadataElement.ExpiresAt, sessionDescriptor.ExpiresAt);

            await _amazonS3.PutObjectAsync(request);

            return sessionDescriptor;
        }

        public async Task<MultipartUploadSession> InitiateMultipartUpload(Guid sessionId, string fileName, string contentType)
        {
            if (!await IsSessionExists(sessionId))
            {
                throw new InvalidOperationException($"Session {sessionId} does not exist");
            }

            var key = BuildKey(sessionId, fileName);
            var request = new InitiateMultipartUploadRequest
                              {
                                  BucketName = _filesBucketName,
                                  Key = key,
                                  ContentType = contentType
                              };
            var metadataWrapper = MetadataCollectionWrapper.For(request.Metadata);
            metadataWrapper.Write(MetadataElement.Filename, fileName);

            var response = await _amazonS3.InitiateMultipartUploadAsync(request);

            return new MultipartUploadSession(sessionId, fileName, response.UploadId);
        }

        public async Task UploadFilePart(MultipartUploadSession uploadSession, Stream inputStream)
        {
            try
            {
                using (var memoryStream = new MemoryStream())
                {
                    await inputStream.CopyToAsync(memoryStream);
                    memoryStream.Position = 0;

                    var key = BuildKey(uploadSession.SessionId, uploadSession.FileName);
                    var response = await _amazonS3.UploadPartAsync(
                                       new UploadPartRequest
                                           {
                                               BucketName = _filesBucketName,
                                               Key = key,
                                               UploadId = uploadSession.UploadId,
                                               InputStream = memoryStream,
                                               PartNumber = uploadSession.NextPartNumber
                                           });
                    uploadSession.AddPart(response.ETag);
                }
            }
            finally
            {
                inputStream.Dispose();
            }
        }

        public async Task AbortMultipartUpload(MultipartUploadSession uploadSession)
        {
            if (!uploadSession.IsCompleted)
            {
                var key = BuildKey(uploadSession.SessionId, uploadSession.FileName);
                await _amazonS3.AbortMultipartUploadAsync(_filesBucketName, key, uploadSession.UploadId);
            }
        }

        public async Task<UploadedFileInfo> CompleteMultipartUpload(MultipartUploadSession uploadSession, long templateId, string templateVersionId, int templateCode)
        {
            var uploadKey = BuildKey(uploadSession.SessionId, uploadSession.FileName);
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

            var templateDescriptor = await _templateStorageReader.GetTemplateDescriptor(templateId, templateVersionId);
            var elementDescriptor = templateDescriptor.Elements.Single(x => x.TemplateCode == templateCode);

            try
            {
                var getResponse = await _amazonS3.GetObjectAsync(_filesBucketName, uploadKey);
                EnsureUploadedFileIsValid(elementDescriptor.Type, elementDescriptor.Constraints, getResponse.ResponseStream);

                var fileKey = string.Concat(BuildKey(uploadSession.SessionId, uploadResponse.ETag), Path.GetExtension(uploadSession.FileName));
                var copyRequest = new CopyObjectRequest
                                      {
                                          SourceBucket = _filesBucketName,
                                          SourceKey = uploadKey,
                                          DestinationBucket = _filesBucketName,
                                          DestinationKey = fileKey,
                                          CannedACL = S3CannedACL.PublicRead
                                      };
                await _amazonS3.CopyObjectAsync(copyRequest);

                return new UploadedFileInfo(uploadResponse.ETag, new Uri(_fileStorageEndpointUri, fileKey));
            }
            finally
            {
                await _amazonS3.DeleteObjectAsync(_filesBucketName, uploadKey);
            }
        }

        private static string BuildKey(Guid sessionId, string fileName) => $"{sessionId}/{fileName}";

        private static void EnsureUploadedFileIsValid(ElementDescriptorType elementDescriptorType, IConstraintSet elementDescriptorConstraints, Stream inputStream)
        {
            switch (elementDescriptorType)
            {
                case ElementDescriptorType.Image:
                    ValidateImage((ImageElementConstraints)elementDescriptorConstraints, inputStream);
                    break;
                case ElementDescriptorType.Article:
                    ValidateArticle((ArticleElementConstraints)elementDescriptorConstraints, inputStream);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementDescriptorType));
            }
        }

        private static void ValidateImage(ImageElementConstraints constraints, Stream inputStream)
        {
            Image image;
            try
            {
                image = new Image(inputStream);
            }
            catch (Exception ex)
            {
                throw new ImageIncorrectException("Image cannot be loaded.", ex);
            }

            if (constraints.SupportedFileFormats.Any(x => image.CurrentImageFormat.Encoder.MimeType.IndexOf(x.ToString(), StringComparison.OrdinalIgnoreCase) < 0))
            {
                throw new ImageIncorrectException("Image has an incorrect format");
            }

            if (image.Width > constraints.ImageSize.Width || image.Height > constraints.ImageSize.Height)
            {
                throw new ImageIncorrectException("Image has an incorrect size");
            }
        }

        private static void ValidateArticle(ArticleElementConstraints constraints, Stream inputStream)
        {
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
    }
}
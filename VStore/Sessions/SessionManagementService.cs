using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using ImageSharp;

using NuClear.VStore.Descriptors.Sessions;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Options;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionManagementService
    {
        private const string SessionToken = "session";

        private readonly Uri _endpointUri;
        private readonly string _filesBucketName;
        private readonly IAmazonS3 _amazonS3;
        private readonly TemplateStorageReader _templateStorageReader;

        public SessionManagementService(Uri endpointUri, CephOptions cephOptions, IAmazonS3 amazonS3, TemplateStorageReader templateStorageReader)
        {
            _endpointUri = endpointUri;
            _filesBucketName = cephOptions.FilesBucketName;
            _amazonS3 = amazonS3;
            _templateStorageReader = templateStorageReader;
        }

        public async Task<SessionDescriptor> Setup(long templateId)
        {
            var templateDescriptor = await _templateStorageReader.GetTemplateDescriptor(templateId, null);
            var sessionDescriptor = new SessionDescriptor(_endpointUri, templateDescriptor);

            if (sessionDescriptor.UploadUris.Count == 0)
            {
                throw new SessionCannotBeCreatedException($"Nothing to upload for template '{templateDescriptor.Id}'");
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

        public async Task<string> InitiateMultipartUpload(
            Guid sessionId,
            long templateId,
            string templateVersionId,
            int templateCode,
            string fileName,
            string contentType)
        {
            var key = BuildKey(sessionId, fileName);
            var request = new InitiateMultipartUploadRequest
                              {
                                  BucketName = _filesBucketName,
                                  Key = key,
                                  ContentType = contentType,
                                  CannedACL = S3CannedACL.PublicRead,
                              };
            var response = await _amazonS3.InitiateMultipartUploadAsync(request);
            await _amazonS3.AbortMultipartUploadAsync(_filesBucketName, key, response.UploadId);

            return response.UploadId;
        }

        public async Task<string> UploadFilePart(
            Guid sessionId,
            string fileName,
            string uploadId,
            int partNumber,
            long? filePosition,
            Stream inputStream)
        {
            var key = BuildKey(sessionId, fileName);
            var response = await _amazonS3.UploadPartAsync(
                               new UploadPartRequest
                                   {
                                       BucketName = _filesBucketName,
                                       Key = key,
                                       UploadId = uploadId,
                                       InputStream = inputStream,
                                       PartNumber = partNumber,
                                       FilePosition = filePosition.Value
                                   });

            return response.ETag;
        }

        public async Task<string> CompleteMultipartUpload(
            Guid sessionId,
            long templateId,
            string templateVersionId,
            int templateCode,
            string fileName,
            string uploadId,
            List<PartETag> etags)
        {
            var response = await _amazonS3.CompleteMultipartUploadAsync(
                               new CompleteMultipartUploadRequest
                                   {
                                       BucketName = _filesBucketName,
                                       Key = BuildKey(sessionId, fileName),
                                       UploadId = uploadId,
                                       PartETags = etags
                                   });

            var templateDescriptor = await _templateStorageReader.GetTemplateDescriptor(templateId, templateVersionId);
            var elementDescriptor = templateDescriptor.Elements.Single(x => x.TemplateCode == templateCode);

            // var stream = ValidateFile(elementDescriptor.Type, elementDescriptor.Constraints, null);

            return response.ETag;
        }

        private static string BuildKey(Guid sessionId, string fileName) => $"{sessionId}/{fileName}";

        private static Stream ValidateFile(ElementDescriptorType elementDescriptorType, IConstraintSet elementDescriptorConstraints, Stream inputStream)
        {
            switch (elementDescriptorType)
            {
                case ElementDescriptorType.Image:
                    return ValidateImage((ImageElementConstraints)elementDescriptorConstraints, inputStream);
                case ElementDescriptorType.Article:
                    return ValidateArticle((ArticleElementConstraints)elementDescriptorConstraints, inputStream);
                default:
                    throw new ArgumentOutOfRangeException(nameof(elementDescriptorType));
            }
        }

        private static Stream ValidateImage(ImageElementConstraints constraints, Stream inputStream)
        {
            var image = new Image(inputStream);

            // validation logic here

            inputStream.Position = 0;
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(inputStream);
                BitConverter.ToString(hash);
            }

            inputStream.Position = 0;
            return inputStream;
        }

        private static Stream ValidateArticle(ArticleElementConstraints constraints, Stream inputStream)
        {
            return inputStream;
        }
    }
}
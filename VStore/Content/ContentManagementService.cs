using System;
using System.Threading.Tasks;

using Amazon.S3;

using NuClear.VStore.Options;

using Amazon.S3.Model;

using Newtonsoft.Json;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Json;
using NuClear.VStore.Locks;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Content
{
    public sealed class ContentManagementService
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly TemplateStorageReader _templateStorageReader;
        private readonly LockSessionFactory _lockSessionFactory;
        private readonly string _bucketName;

        public ContentManagementService(
            CephOptions cephOptions,
            IAmazonS3 amazonS3,
            TemplateStorageReader templateStorageReader,
            LockSessionFactory lockSessionFactory)
        {
            _amazonS3 = amazonS3;
            _templateStorageReader = templateStorageReader;
            _lockSessionFactory = lockSessionFactory;
            _bucketName = cephOptions.ContentBucketName;
        }

        public async Task<string> Initialize(long rootObjectId, IVersionedTemplateDescriptor templateDescriptor)
        {
            if (!await _templateStorageReader.IsTemplateExists(templateDescriptor.Id))
            {
                throw new InvalidOperationException($"Template '{templateDescriptor.Id}' does not exist");
            }

            var latestTemplateVersionId = await _templateStorageReader.GetTemplateLatestVersion(templateDescriptor.Id);
            if (!templateDescriptor.VersionId.Equals(latestTemplateVersionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Provided template descriptor has an outdated version. " +
                                                    $"Latest versionId for template '{templateDescriptor.Id}' is '{latestTemplateVersionId}'");
            }

            using (_lockSessionFactory.CreateLockSession(rootObjectId))
            {
                await SaveTemplate(rootObjectId, templateDescriptor);
                return await InitializeDescriptor(rootObjectId, templateDescriptor.VersionId);
            }
        }

        private async Task SaveTemplate(long rootObjectId, IVersionedTemplateDescriptor templateDescriptor)
        {
            var key = rootObjectId.AsS3ObjectKey(Tokens.TemplatePostfix, templateDescriptor.Id);
            var content = JsonConvert.SerializeObject(templateDescriptor.ElementDescriptors, SerializerSettings.Default);
            var putRequest = new PutObjectRequest
                {
                    Key = key,
                    BucketName = _bucketName,
                    ContentType = ContentType.Json,
                    ContentBody = content,
                    CannedACL = S3CannedACL.PublicRead
                };

            var metadataWrapper = MetadataCollectionWrapper.For(putRequest.Metadata);
            metadataWrapper.Write(MetadataElement.VersionId, templateDescriptor.VersionId);
            metadataWrapper.Write(MetadataElement.Name, templateDescriptor.Name);

            await _amazonS3.PutObjectAsync(putRequest);
        }

        private async Task<string> InitializeDescriptor(long rootObjectId, string templateVersionId)
        {
            var key = rootObjectId.AsS3ObjectKey(Tokens.DescriptorObjectName);
            var putRequest = new PutObjectRequest
                {
                    Key = key,
                    BucketName = _bucketName,
                    ContentType = ContentType.Json,
                    ContentBody = string.Empty,
                    CannedACL = S3CannedACL.PublicRead
                };

            var metadataWrapper = MetadataCollectionWrapper.For(putRequest.Metadata);
            metadataWrapper.Write(MetadataElement.TemplateVersionId, templateVersionId);

            await _amazonS3.PutObjectAsync(putRequest);

            // ceph does not return version-id response header, so we need to do another request to get version
            var versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, key);
            return versionsResponse.Versions.Find(x => x.IsLatest).VersionId;
        }
    }
}
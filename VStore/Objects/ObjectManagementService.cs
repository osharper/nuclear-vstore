using System;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;
using NuClear.VStore.Locks;
using NuClear.VStore.Options;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Objects
{
    public sealed class ObjectManagementService
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly TemplateStorageReader _templateStorageReader;
        private readonly LockSessionFactory _lockSessionFactory;
        private readonly string _bucketName;

        public ObjectManagementService(
            CephOptions cephOptions,
            IAmazonS3 amazonS3,
            TemplateStorageReader templateStorageReader,
            LockSessionFactory lockSessionFactory)
        {
            _amazonS3 = amazonS3;
            _templateStorageReader = templateStorageReader;
            _lockSessionFactory = lockSessionFactory;
            _bucketName = cephOptions.ObjectsBucketName;
        }

        public async Task<string> Create(long id, IObjectDescriptor objectDescriptor)
        {
            if (!await _templateStorageReader.IsTemplateExists(objectDescriptor.TemplateId))
            {
                throw new InvalidOperationException($"Template '{objectDescriptor.TemplateId}' does not exist");
            }

            var latestTemplateVersionId = await _templateStorageReader.GetTemplateLatestVersion(objectDescriptor.TemplateId);
            if (!objectDescriptor.TemplateVersionId.Equals(latestTemplateVersionId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Provided template descriptor has an outdated version. " +
                                                    $"Latest versionId for template '{objectDescriptor.TemplateId}' is '{latestTemplateVersionId}'");
            }

            using (_lockSessionFactory.CreateLockSession(id))
            {
                // await SaveTemplate(id, templateId, templateDescriptor);
                // return await InitializeDescriptor(id, templateDescriptor.VersionId);

                return null;
            }
        }

        public async Task<string> SetTitle(long rootObjectId, string rootObjectVersionId, string title)
        {
            using (_lockSessionFactory.CreateLockSession(rootObjectId))
            {
                var descriptorKey = rootObjectId.AsS3ObjectKey(Tokens.DescriptorObjectName);
                await EnsureObjectState(descriptorKey, rootObjectVersionId);

                var metadataResponse = await _amazonS3.GetObjectMetadataAsync(_bucketName, descriptorKey, rootObjectVersionId);

                var metadataWrapper = MetadataCollectionWrapper.For(metadataResponse.Metadata);
                metadataWrapper.Write(MetadataElement.Title, title);
                var metadata = metadataWrapper.Unwrap();

                var copyRequest = new CopyObjectRequest
                {
                    SourceBucket = _bucketName,
                    DestinationBucket = _bucketName,
                    SourceKey = descriptorKey,
                    DestinationKey = descriptorKey,
                    SourceVersionId = rootObjectVersionId,
                    MetadataDirective = S3MetadataDirective.REPLACE,
                    CannedACL = S3CannedACL.PublicRead
                };
                foreach (var metadataKey in metadata.Keys)
                {
                    copyRequest.Metadata[metadataKey] = metadata[metadataKey];
                }

                await _amazonS3.CopyObjectAsync(copyRequest);

                var versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, descriptorKey);
                return versionsResponse.Versions.Find(x => x.IsLatest).VersionId;
            }
        }

        public async Task<string> ModifyElement(long rootObjectId, string rootObjectVersionId, long elementId, string content)
        {
            using (_lockSessionFactory.CreateLockSession(rootObjectId))
            {
                var descriptorKey = rootObjectId.AsS3ObjectKey(Tokens.DescriptorObjectName);
                await EnsureObjectState(descriptorKey, rootObjectVersionId);
            }

            return string.Empty;
        }

        private async Task SaveTemplate(long rootObjectId, long templateId, IVersionedTemplateDescriptor templateDescriptor)
        {
            var key = rootObjectId.AsS3ObjectKey(Tokens.TemplatePostfix, templateId);
            var content = JsonConvert.SerializeObject(templateDescriptor.Elements, SerializerSettings.Default);
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

        private async Task EnsureObjectState(string key, string versionId)
        {
            var versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, key);
            if (versionsResponse.Versions.Count == 0)
            {
                throw new ObjectNotFoundException($"Object '{key}' not found");
            }

            var latestVersionId = versionsResponse.Versions.Find(x => x.IsLatest).VersionId;
            if (!versionId.Equals(latestVersionId, StringComparison.Ordinal))
            {
                throw new ConcurrencyException(key, versionId, latestVersionId);
            }
        }
    }
}
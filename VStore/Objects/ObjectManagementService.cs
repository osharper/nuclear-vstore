using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Json;
using NuClear.VStore.Locks;
using NuClear.VStore.Options;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Objects
{
    public sealed class ObjectManagementService
    {
        private const string ObjectToken = "object";

        private readonly IAmazonS3 _amazonS3;
        private readonly TemplateStorageReader _templateStorageReader;
        private readonly ObjectStorageReader _objectStorageReader;
        private readonly LockSessionFactory _lockSessionFactory;
        private readonly string _bucketName;

        public ObjectManagementService(
            CephOptions cephOptions,
            IAmazonS3 amazonS3,
            TemplateStorageReader templateStorageReader,
            ObjectStorageReader objectStorageReader,
            LockSessionFactory lockSessionFactory)
        {
            _amazonS3 = amazonS3;
            _templateStorageReader = templateStorageReader;
            _objectStorageReader = objectStorageReader;
            _lockSessionFactory = lockSessionFactory;
            _bucketName = cephOptions.ObjectsBucketName;
        }

        public async Task<string> Create(long id, IObjectDescriptor objectDescriptor)
        {
            if (await _objectStorageReader.IsObjectExists(id))
            {
                throw new InvalidOperationException($"Object '{id}' already exists");
            }

            if (!await _templateStorageReader.IsTemplateExists(objectDescriptor.TemplateId))
            {
                throw new InvalidOperationException($"Template '{objectDescriptor.TemplateId}' does not exist");
            }

            var latestTemplateVersionId = await _templateStorageReader.GetTemplateLatestVersion(objectDescriptor.TemplateId);
            if (!objectDescriptor.TemplateVersionId.Equals(latestTemplateVersionId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Template '{objectDescriptor.TemplateId}' has an outdated version. " +
                                                    $"Latest versionId for template '{objectDescriptor.TemplateId}' is '{latestTemplateVersionId}'");
            }

            return await PutObject(id, objectDescriptor);
        }

        public async Task<string> ModifyElement(long rootObjectId, string rootObjectVersionId, long elementId, string content)
        {
            using (_lockSessionFactory.CreateLockSession(rootObjectId))
            {
                var descriptorKey = rootObjectId.AsS3ObjectKey(Tokens.ObjectPostfix);
                await EnsureObjectState(descriptorKey, rootObjectVersionId);
            }

            return string.Empty;
        }

        public void VerifyObjectElementsConsistency(long? objectId, IEnumerable<IObjectElementDescriptor> elementDescriptors)
        {
        }

        private async Task<string> PutObject(long id, IObjectDescriptor objectDescriptor)
        {
            VerifyObjectElementsConsistency(id, objectDescriptor.Elements);

            PutObjectRequest putRequest;
            foreach (var elementDescriptor in objectDescriptor.Elements)
            {
                putRequest = new PutObjectRequest
                                 {
                                     Key = id.AsS3ObjectKey(elementDescriptor.Id),
                                     BucketName = _bucketName,
                                     ContentType = ContentType.Json,
                                     ContentBody = JsonConvert.SerializeObject(elementDescriptor, SerializerSettings.Default),
                                     CannedACL = S3CannedACL.PublicRead
                                 };
                await _amazonS3.PutObjectAsync(putRequest);
            }

            var objectVersions = await _objectStorageReader.GetObjectLatestVersions(id);
            var objectPersistenceDescriptor = new ObjectPersistenceDescriptor
                                                  {
                                                      TemplateId = objectDescriptor.TemplateId,
                                                      TemplateVersionId = objectDescriptor.TemplateVersionId,
                                                      Properties = objectDescriptor.Properties,
                                                      Elements = objectVersions
                                                  };

            var objectKey = id.AsS3ObjectKey(Tokens.ObjectPostfix);
            putRequest = new PutObjectRequest
                             {
                                 Key = objectKey,
                                 BucketName = _bucketName,
                                 ContentType = ContentType.Json,
                                 ContentBody = JsonConvert.SerializeObject(objectPersistenceDescriptor, SerializerSettings.Default),
                                 CannedACL = S3CannedACL.PublicRead
                             };
            await _amazonS3.PutObjectAsync(putRequest);

            objectVersions = await _objectStorageReader.GetObjectLatestVersions(id);
            return objectVersions.Where(x => x.Key.Equals(objectKey, StringComparison.OrdinalIgnoreCase)).Select(x => x.VersionId).Single();
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
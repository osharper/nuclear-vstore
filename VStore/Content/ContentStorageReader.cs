using System;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Options;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Content
{
    public sealed class ContentStorageReader
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly TemplateStorageReader _templateStorageReader;
        private readonly string _bucketName;

        public ContentStorageReader(
            CephOptions cephOptions,
            IAmazonS3 amazonS3,
            TemplateStorageReader templateStorageReader)
        {
            _amazonS3 = amazonS3;
            _templateStorageReader = templateStorageReader;
            _bucketName = cephOptions.ContentBucketName;
        }

        public async Task<IVersionedTemplateDescriptor> GetTemplateDescriptor(long id, string versionId)
        {
            var listResponse = await _amazonS3.ListObjectsV2Async(
                                   new ListObjectsV2Request
                                       {
                                           BucketName = _bucketName,
                                           Prefix = id.AsS3ObjectKey(Tokens.TemplatePostfix)
                                       });
            if (listResponse.S3Objects.Count == 0)
            {
                throw new ObjectNotFoundException($"Template for object {id} not found");
            }

            if (listResponse.S3Objects.Count > 1)
            {
                throw new ObjectInconsistentException(id, $"More than one template found");
            }

            var templateId = listResponse.S3Objects[0].Key.AsObjectId();

            var metadataResponse = await _amazonS3.GetObjectMetadataAsync(_bucketName, id.AsS3ObjectKey(Tokens.DescriptorObjectName), versionId);
            var metadataWrapper = MetadataCollectionWrapper.For(metadataResponse.Metadata);
            var templateVersionId = metadataWrapper.Read<string>(MetadataElement.TemplateVersionId);

            if (string.IsNullOrEmpty(templateVersionId))
            {
                throw new ObjectInconsistentException(id, "Template version cannot be determined");
            }

            return await _templateStorageReader.GetTemplateDescriptor(new Guid(templateId), templateVersionId);
        }
    }
}
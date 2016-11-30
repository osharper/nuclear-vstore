using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;
using NuClear.VStore.Options;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Objects
{
    public sealed class ObjectStorageReader
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly TemplateStorageReader _templateStorageReader;
        private readonly string _bucketName;

        public ObjectStorageReader(
            CephOptions cephOptions,
            IAmazonS3 amazonS3,
            TemplateStorageReader templateStorageReader)
        {
            _amazonS3 = amazonS3;
            _templateStorageReader = templateStorageReader;
            _bucketName = cephOptions.ObjectsBucketName;
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
                throw new ObjectNotFoundException($"Template for the object '{id}' not found");
            }

            if (listResponse.S3Objects.Count > 1)
            {
                throw new ObjectInconsistentException(id, $"More than one template found for the object '{id}'");
            }

            var templateId = listResponse.S3Objects[0].Key.AsObjectId();

            var metadataResponse = await _amazonS3.GetObjectMetadataAsync(_bucketName, id.AsS3ObjectKey(Tokens.DescriptorObjectName), versionId);
            var metadataWrapper = MetadataCollectionWrapper.For(metadataResponse.Metadata);
            var templateVersionId = metadataWrapper.Read<string>(MetadataElement.TemplateVersionId);

            if (string.IsNullOrEmpty(templateVersionId))
            {
                throw new ObjectInconsistentException(id, "Template version cannot be determined");
            }

            return await _templateStorageReader.GetTemplateDescriptor(long.Parse(templateId), templateVersionId);
        }

        public async Task<string> GetObjectLatestVersion(long id)
        {
            var versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, id.ToString());
            return versionsResponse.Versions.Find(x => x.IsLatest).VersionId;
        }

        public async Task<ObjectDescriptor> GetObjectDescriptor(long id, string versionId)
        {
            var objectVersionId = string.IsNullOrEmpty(versionId) ? await GetObjectLatestVersion(id) : versionId;

            var response = await _amazonS3.GetObjectAsync(_bucketName, id.AsS3ObjectKey(Tokens.DescriptorObjectName), objectVersionId);
            if (response.ResponseStream == null)
            {
                throw new ObjectNotFoundException($"ImmutableDescriptor for the version '{versionId}' of the object '{id}' not found");
            }

            var descriptor = DescriptorBuilder.For(id)
                                              .WithVersion(objectVersionId)
                                              .WithLastModified(response.LastModified)
                                              .Build<ObjectDescriptor>();

            string content;
            using (var reader = new StreamReader(response.ResponseStream, Encoding.UTF8))
            {
                content = reader.ReadToEnd();
            }

            // descriptor.ContentElementDescriptors = JsonConvert.DeserializeObject<IReadOnlyCollection<IObjectElementDescriptor>>(content, SerializerSettings.Default);
            // descriptor.TemplateDescriptor = await GetTemplateDescriptor(id, objectVersionId);

            return descriptor;
        }
    }
}
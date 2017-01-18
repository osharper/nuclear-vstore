using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;
using NuClear.VStore.Options;
using NuClear.VStore.S3;

namespace NuClear.VStore.Templates
{
    public sealed class TemplatesStorageReader
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName;

        public TemplatesStorageReader(CephOptions cephOptions, IAmazonS3 amazonS3)
        {
            _amazonS3 = amazonS3;
            _bucketName = cephOptions.TemplatesBucketName;
        }

        public async Task<IReadOnlyCollection<IdentifyableObjectDescriptor>> GetTemplateMetadatas()
        {
            var listVersionsResponse = await _amazonS3.ListVersionsAsync(_bucketName);

            var descriptors = new ConcurrentBag<IdentifyableObjectDescriptor>();
            Parallel.ForEach(
                listVersionsResponse.Versions.FindAll(x => x.IsLatest && !x.IsDeleteMarker),
                obj => descriptors.Add(new IdentifyableObjectDescriptor(obj.Key, obj.VersionId, obj.LastModified)));

            return descriptors.OrderBy(x => x.LastModified).ToArray();
        }

        public async Task<TemplateDescriptor> GetTemplateDescriptor(long id, string versionId)
        {
            var objectVersionId = string.IsNullOrEmpty(versionId) ? await GetTemplateLatestVersion(id) : versionId;

            GetObjectResponse response;
            try
            {
                response = await _amazonS3.GetObjectAsync(_bucketName, id.ToString(), objectVersionId);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ObjectNotFoundException($"Template '{id}' version '{objectVersionId}' not found");
            }

            var metadataWrapper = MetadataCollectionWrapper.For(response.Metadata);
            var author = metadataWrapper.Read<string>(MetadataElement.Author);

            string json;
            using (var reader = new StreamReader(response.ResponseStream, Encoding.UTF8))
            {
                json = reader.ReadToEnd();
            }

            var descriptor = new TemplateDescriptor { Id = id, VersionId = objectVersionId, LastModified = response.LastModified, Author = author };
            JsonConvert.PopulateObject(json, descriptor, SerializerSettings.Default);

            return descriptor;
        }

        public async Task<string> GetTemplateLatestVersion(long id)
        {
            var versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, id.ToString());
            var version = versionsResponse.Versions.Find(x => x.IsLatest);
            if (version == null)
            {
                throw new ObjectNotFoundException($"Template '{id}' versions not found");
            }

            return version.VersionId;
        }

        public async Task<bool> IsTemplateExists(long id)
        {
            var listResponse = await _amazonS3.ListObjectsV2Async(
                                   new ListObjectsV2Request
                                       {
                                           BucketName = _bucketName,
                                           Prefix = id.ToString()
                                       });
            return listResponse.S3Objects.Count != 0;
        }
    }
}
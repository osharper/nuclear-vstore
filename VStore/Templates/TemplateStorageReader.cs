using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Json;
using NuClear.VStore.Options;
using NuClear.VStore.S3;

namespace NuClear.VStore.Templates
{
    public sealed class TemplateStorageReader
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName;

        public TemplateStorageReader(CephOptions cephOptions, IAmazonS3 amazonS3)
        {
            _amazonS3 = amazonS3;
            _bucketName = cephOptions.TemplatesBucketName;
        }

        public async Task<IReadOnlyCollection<TemplateDescriptor>> GetAllTemplateDescriptors()
        {
            var listVersionsResponse = await _amazonS3.ListVersionsAsync(_bucketName);

            var descriptors = new ConcurrentBag<TemplateDescriptor>();
            Parallel.ForEach(
                listVersionsResponse.Versions.FindAll(x => x.IsLatest && !x.IsDeleteMarker),
                obj =>
                    {
                        var response = _amazonS3.GetObjectMetadataAsync(_bucketName, obj.Key, obj.VersionId);
                        var metadata = response.Result.Metadata;

                        var descriptor = TemplateDescriptorBuilder.For(obj.Key)
                                                                  .WithVersion(obj.VersionId)
                                                                  .WithLastModified(obj.LastModified)
                                                                  .WithMetadata(metadata)
                                                                  .Build();
                        descriptors.Add(descriptor);
                    });

            return descriptors.OrderBy(x => x.LastModified).ToArray();
        }

        public async Task<TemplateDescriptor> GetTemplateDescriptor(Guid id, string versionId)
        {
            var objectVersionId = string.IsNullOrEmpty(versionId) ? await GetTemplateLatestVersion(id) : versionId;

            var response = await _amazonS3.GetObjectAsync(_bucketName, id.ToString(), objectVersionId);
            var descriptor = TemplateDescriptorBuilder.For(id)
                                                      .WithVersion(objectVersionId)
                                                      .WithLastModified(response.LastModified)
                                                      .WithMetadata(response.Metadata)
                                                      .Build();

            string content;
            using (var reader = new StreamReader(response.ResponseStream, Encoding.UTF8))
            {
                content = reader.ReadToEnd();
            }

            var descriptors = JsonConvert.DeserializeObject<IReadOnlyCollection<IElementDescriptor>>(content, new TemplateDescriptorJsonConverter());
            foreach (var elementDescriptor in descriptors)
            {
                descriptor.AddElementDescriptor(elementDescriptor);
            }

            return descriptor;
        }

        public async Task<string> GetTemplateLatestVersion(Guid id)
        {
            var versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, id.ToString());
            return versionsResponse.Versions.Find(x => x.IsLatest).VersionId;
        }

        public async Task<bool> IsTemplateExists(Guid id)
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
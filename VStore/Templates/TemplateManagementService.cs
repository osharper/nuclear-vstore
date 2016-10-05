using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Extensions;
using NuClear.VStore.Json;
using NuClear.VStore.Locks;
using NuClear.VStore.Options;

namespace NuClear.VStore.Templates
{
    public sealed class TemplateManagementService
    {
        private const string NameToken = "name";
        private const string IsRequiredToken = "isrequired";

        private readonly IAmazonS3 _amazonS3;
        private readonly LockSessionFactory _lockSessionFactory;
        private readonly string _bucketName;

        public TemplateManagementService(
            CephOptions cephOptions,
            IAmazonS3 amazonS3,
            LockSessionFactory lockSessionFactory)
        {
            _amazonS3 = amazonS3;
            _lockSessionFactory = lockSessionFactory;
            _bucketName = cephOptions.TemplatesBucketName;
        }

        public IReadOnlyCollection<IElementDescriptor> GetAvailableElementDescriptors()
        {
            return new IElementDescriptor[]
                {
                    new TextElementDescriptor(),
                    new ImageElementDescriptor(),
                    new ArticleElementDescriptor()
                };
        }

        public async Task<IReadOnlyCollection<TemplateDescriptor>> GetAllTemplateDescriptors()
        {
            var listVersionsResponse = await _amazonS3.ListVersionsAsync(_bucketName);

            var descriptors = new ConcurrentBag<TemplateDescriptor>();
            var versions = listVersionsResponse.Versions.FindAll(x => x.IsLatest && !x.IsDeleteMarker);
            versions.Sort((x, y) => x.LastModified > y.LastModified ? 1 : -1);
            Parallel.ForEach(
                versions,
                obj =>
                    {
                        var response = _amazonS3.GetObjectMetadataAsync(_bucketName, obj.Key, obj.VersionId);
                        var metadata = response.Result.Metadata;

                        var name = Encoding.UTF8.GetString(Convert.FromBase64String(metadata[NameToken.AsMetadata()]));
                        var isRequired = bool.Parse(metadata[IsRequiredToken.AsMetadata()]);
                        descriptors.Add(new TemplateDescriptor
                                            {
                                                Id = Guid.Parse(obj.Key),
                                                VersionId = obj.VersionId,
                                                Name = name,
                                                IsRequired = isRequired
                                            });
                    });

            return descriptors;
        }

        public async Task<TemplateDescriptor> GetTemplateDescriptor(Guid id, string versionId)
        {
            string objectVersionId;
            if (string.IsNullOrEmpty(versionId))
            {
                var versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, id.ToString());
                objectVersionId = versionsResponse.Versions.Find(x => x.IsLatest).VersionId;
            }
            else
            {
                objectVersionId = versionId;
            }

            var response = await _amazonS3.GetObjectAsync(_bucketName, id.ToString(), objectVersionId);
            var descriptor = new TemplateDescriptor
                {
                    Id = id,
                    VersionId = objectVersionId,
                    Name = response.Metadata[NameToken.AsMetadata()],
                    IsRequired = bool.Parse(response.Metadata[IsRequiredToken.AsMetadata()])
                };

            string content;
            using (var reader = new StreamReader(response.ResponseStream, Encoding.UTF8))
            {
                content = reader.ReadToEnd();
            }

            var descriptors = JsonConvert.DeserializeObject<IEnumerable<IElementDescriptor>>(content, new TemplateDescriptorJsonConverter());
            foreach (var elementDescriptor in descriptors)
            {
                descriptor.ElementDescriptors.Add(elementDescriptor);
            }

            return descriptor;
        }

        public async Task<string> CreateTemplate(TemplateDescriptor templateDescriptor)
        {
            if (templateDescriptor.Id == Guid.Empty)
            {
                throw new InvalidOperationException($"Template Id must be set to the value different from '{templateDescriptor.Id}'");
            }

            if (!string.IsNullOrEmpty(templateDescriptor.VersionId))
            {
                throw new InvalidOperationException("VersionId must not be set");
            }

            using (_lockSessionFactory.CreateLockSession(templateDescriptor.Id))
            {
                var id = templateDescriptor.Id.ToString();
                var listResponse = await _amazonS3.ListObjectsV2Async(
                                       new ListObjectsV2Request
                                           {
                                               BucketName = _bucketName,
                                               Prefix = id
                                           });
                if (listResponse.S3Objects.Count != 0 || !string.IsNullOrEmpty(templateDescriptor.VersionId))
                {
                    throw new InvalidOperationException($"Template with Id '{templateDescriptor.Id}' already exists");
                }

                await PutTemplate(id, templateDescriptor.Name, templateDescriptor.IsRequired, templateDescriptor.ElementDescriptors);

                // ceph does not return version-id response header, so we need to do another request to get version
                var versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, id);
                return versionsResponse.Versions[0].VersionId;
            }
        }

        public async Task<string> ModifyTemplate(TemplateDescriptor templateDescriptor)
        {
            if (templateDescriptor.Id == Guid.Empty)
            {
                throw new InvalidOperationException($"Template Id must be set to the value different from '{templateDescriptor.Id}'");
            }

            if (string.IsNullOrEmpty(templateDescriptor.VersionId))
            {
                throw new InvalidOperationException("VersionId must be set");
            }

            using (_lockSessionFactory.CreateLockSession(templateDescriptor.Id))
            {
                var id = templateDescriptor.Id.ToString();
                var listResponse = await _amazonS3.ListObjectsV2Async(
                                       new ListObjectsV2Request
                                           {
                                               BucketName = _bucketName,
                                               Prefix = id
                                           });
                if (listResponse.S3Objects.Count == 0 || string.IsNullOrEmpty(templateDescriptor.VersionId))
                {
                    throw new InvalidOperationException($"Template with Id '{templateDescriptor.Id}' does not exist");
                }

                var versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, id);
                var latestVersionId = versionsResponse.Versions.Find(x => x.IsLatest).VersionId;
                if (!templateDescriptor.VersionId.Equals(latestVersionId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Template with Id '{templateDescriptor.Id}' cannot be modified. " +
                                                        $"Reason: versionId '{templateDescriptor.VersionId}' has been overwritten. " +
                                                        $"Latest versionId is '{latestVersionId}'");
                }

                await PutTemplate(id, templateDescriptor.Name, templateDescriptor.IsRequired, templateDescriptor.ElementDescriptors);

                // ceph does not return version-id response header, so we need to do another request to get version
                versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, id);
                return versionsResponse.Versions.Find(x => x.IsLatest).VersionId;
            }
        }

        private async Task PutTemplate(string id, string name, bool isRequired, IEnumerable<IElementDescriptor> elementDescriptors)
        {
            var encodedName = Convert.ToBase64String(Encoding.UTF8.GetBytes(name));

            var content = JsonConvert.SerializeObject(
                elementDescriptors,
                new JsonSerializerSettings
                    {
                        Culture = CultureInfo.InvariantCulture,
                        ContractResolver = new CamelCasePropertyNamesContractResolver()
                    });

            var putRequest = new PutObjectRequest
                {
                    Key = id,
                    BucketName = _bucketName,
                    ContentType = "application/javascript",
                    ContentBody = content,
                    CannedACL = S3CannedACL.PublicRead,
                    Metadata =
                        {
                            [NameToken.AsMetadata()] = encodedName,
                            [IsRequiredToken.AsMetadata()] = isRequired.ToString()
                        }
                };

            await _amazonS3.PutObjectAsync(putRequest);
        }
    }
}
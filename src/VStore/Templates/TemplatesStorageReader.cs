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
        private readonly IAmazonS3Proxy _amazonS3;
        private readonly string _bucketName;
        private readonly int _degreeOfParallelism;

        public TemplatesStorageReader(CephOptions cephOptions, IAmazonS3Proxy amazonS3)
        {
            _amazonS3 = amazonS3;
            _bucketName = cephOptions.TemplatesBucketName;
            _degreeOfParallelism = cephOptions.DegreeOfParallelism;
        }

        public async Task<ContinuationContainer<IdentifyableObjectDescriptor<long>>> GetTemplateMetadatas(string continuationToken)
        {
            var listResponse = await _amazonS3.ListObjectsAsync(new ListObjectsRequest { BucketName = _bucketName, Marker = continuationToken });

            var descriptors = listResponse.S3Objects.Select(x => new IdentifyableObjectDescriptor<long>(long.Parse(x.Key), x.LastModified)).ToArray();
            return new ContinuationContainer<IdentifyableObjectDescriptor<long>>(descriptors, listResponse.NextMarker);
        }

        public async Task<IReadOnlyCollection<ModifiedTemplateDescriptor>> GetTemplateMetadatas(IReadOnlyCollection<long> ids)
        {
            var uniqueIds = new HashSet<long>(ids);
            var partitioner = Partitioner.Create(uniqueIds);
            var result = new ModifiedTemplateDescriptor[uniqueIds.Count];
            var tasks = partitioner
                .GetOrderablePartitions(_degreeOfParallelism)
                .Select(async x =>
                            {
                                while (x.MoveNext())
                                {
                                    var templateId = x.Current.Value;
                                    ModifiedTemplateDescriptor descriptor;
                                    try
                                    {
                                        var response = await _amazonS3.GetObjectMetadataAsync(_bucketName, templateId.ToString());
                                        var metadataWrapper = MetadataCollectionWrapper.For(response.Metadata);
                                        var author = metadataWrapper.Read<string>(MetadataElement.Author);
                                        var authorLogin = metadataWrapper.Read<string>(MetadataElement.AuthorLogin);
                                        var authorName = metadataWrapper.ReadEncoded<string>(MetadataElement.AuthorName);

                                        var versionId = await GetTemplateLatestVersion(templateId);
                                        descriptor = new ModifiedTemplateDescriptor(
                                            x.Current.Value,
                                            versionId,
                                            response.LastModified,
                                            new AuthorInfo(author, authorLogin, authorName));
                                    }
                                    catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                                    {
                                        descriptor = null;
                                    }

                                    result[x.Current.Key] = descriptor;
                                }
                            });
            await Task.WhenAll(tasks);
            return result.Where(x => x != null).ToList();
        }

        public async Task<TemplateDescriptor> GetTemplateDescriptor(long id, string versionId)
        {
            var objectVersionId = string.IsNullOrEmpty(versionId) ? await GetTemplateLatestVersion(id) : versionId;

            try
            {
                using (var response = await _amazonS3.GetObjectAsync(_bucketName, id.ToString(), objectVersionId))
                {
                    var metadataWrapper = MetadataCollectionWrapper.For(response.Metadata);
                    var author = metadataWrapper.Read<string>(MetadataElement.Author);
                    var authorLogin = metadataWrapper.Read<string>(MetadataElement.AuthorLogin);
                    var authorName = metadataWrapper.ReadEncoded<string>(MetadataElement.AuthorName);

                    string json;
                    using (var reader = new StreamReader(response.ResponseStream, Encoding.UTF8))
                    {
                        json = reader.ReadToEnd();
                    }

                    var descriptor = new TemplateDescriptor
                        {
                            Id = id,
                            VersionId = objectVersionId,
                            LastModified = response.LastModified,
                            Author = author,
                            AuthorLogin = authorLogin,
                            AuthorName = authorName
                        };
                    JsonConvert.PopulateObject(json, descriptor, SerializerSettings.Default);

                    return descriptor;
                }
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ObjectNotFoundException($"Template '{id}' version '{objectVersionId}' not found");
            }
        }

        public async Task<string> GetTemplateLatestVersion(long id)
        {
            var idAsString = id.ToString();
            var versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, idAsString);
            var version = versionsResponse.Versions.Find(x => x.Key == idAsString && !x.IsDeleteMarker && x.IsLatest);
            if (version == null)
            {
                throw new ObjectNotFoundException($"Template '{id}' versions not found");
            }

            return version.VersionId;
        }

        public async Task<bool> IsTemplateExists(long id)
        {
            var idAsString = id.ToString();
            var listResponse = await _amazonS3.ListObjectsV2Async(
                                    new ListObjectsV2Request
                                        {
                                            BucketName = _bucketName,
                                            Prefix = idAsString
                                    });
            return listResponse.S3Objects.Any(o => o.Key == idAsString);
        }
    }
}

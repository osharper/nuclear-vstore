using System;
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
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Objects.Persistence;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;
using NuClear.VStore.Options;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Objects
{
    public sealed class ObjectsStorageReader
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly TemplatesStorageReader _templatesStorageReader;
        private readonly string _bucketName;
        private readonly int _degreeOfParallelism;
        private readonly Uri _fileStorageEndpoint;

        public ObjectsStorageReader(
            CephOptions cephOptions,
            VStoreOptions vStoreOptions,
            IAmazonS3 amazonS3,
            TemplatesStorageReader templatesStorageReader)
        {
            _amazonS3 = amazonS3;
            _templatesStorageReader = templatesStorageReader;
            _bucketName = cephOptions.ObjectsBucketName;
            _degreeOfParallelism = cephOptions.DegreeOfParallelism;
            _fileStorageEndpoint = vStoreOptions.FileStorageEndpoint;
        }

        public async Task<ContinuationContainer<IdentifyableObjectDescriptor<long>>> GetObjectMetadatas(string continuationToken)
        {
            var listResponse = await _amazonS3.ListObjectsAsync(new ListObjectsRequest { BucketName = _bucketName, Marker = continuationToken });

            var descriptors = listResponse.S3Objects.Select(x => new IdentifyableObjectDescriptor<long>(x.Key.AsRootObjectId(), x.LastModified)).Distinct().ToArray();
            return new ContinuationContainer<IdentifyableObjectDescriptor<long>>(descriptors, listResponse.NextMarker);
        }

        public async Task<IVersionedTemplateDescriptor> GetTemplateDescriptor(long id, string versionId)
        {
            (var persistenceDescriptor, var _, var _) =
                await GetObjectFromS3<ObjectPersistenceDescriptor>(id.AsS3ObjectKey(Tokens.ObjectPostfix), versionId);
            return await _templatesStorageReader.GetTemplateDescriptor(persistenceDescriptor.TemplateId, persistenceDescriptor.TemplateVersionId);
        }

        public async Task<IReadOnlyCollection<ModifiedObjectDescriptor>> GetObjectVersions(long id, string initialVersionId)
        {
            var versions = new List<ObjectVersion>();

            async Task<(IReadOnlyCollection<int> ModifiedElements, AuthorInfo AuthorInfo)> GetElementMetadata(string key, string versionId)
            {
                var metadataResponse = await _amazonS3.GetObjectMetadataAsync(_bucketName, key, versionId);

                var metadataWrapper = MetadataCollectionWrapper.For(metadataResponse.Metadata);
                var modifiedElements = metadataWrapper.Read<string>(MetadataElement.ModifiedElements);
                var modifiedElementIds = string.IsNullOrEmpty(modifiedElements)
                                             ? Array.Empty<int>()
                                             : modifiedElements.Split(Tokens.ModifiedElementsDelimiter).Select(int.Parse).ToArray();
                var author = metadataWrapper.Read<string>(MetadataElement.Author);
                var authorLogin = metadataWrapper.Read<string>(MetadataElement.AuthorLogin);
                var authorName = metadataWrapper.Read<string>(MetadataElement.AuthorName);
                return (modifiedElementIds, new AuthorInfo(author, authorLogin, authorName));
            }

            async Task<(bool IsTruncated, int NextVersionIndex, string NextVersionIdMarker)> ListVersions(int nextVersionIndex, string nextVersionIdMarker)
            {
                var versionsResponse = await _amazonS3.ListVersionsAsync(
                                           new ListVersionsRequest
                                               {
                                                   BucketName = _bucketName,
                                                   Prefix = id.AsS3ObjectKey(Tokens.ObjectPostfix),
                                                   VersionIdMarker = nextVersionIdMarker
                                               });

                var initialVersionIdReached = false;
                var versionInfos = versionsResponse
                    .Versions
                    .Where(x => !x.IsDeleteMarker)
                    .Aggregate(
                        new List<(string Key, string VersionId, int VersionIndex, DateTime LastModified)>(),
                        (list, next) =>
                            {
                                initialVersionIdReached = initialVersionIdReached ||
                                                          !string.IsNullOrEmpty(initialVersionId) &&
                                                          initialVersionId.Equals(next.VersionId, StringComparison.OrdinalIgnoreCase);
                                if (!initialVersionIdReached)
                                {
                                    list.Add((next.Key, next.VersionId, nextVersionIndex, next.LastModified));
                                }

                                ++nextVersionIndex;

                                return list;
                            });

                var objectVersions = new ObjectVersion[versionInfos.Count];
                var partitioner = Partitioner.Create(versionInfos);
                var tasks = partitioner.GetOrderablePartitions(_degreeOfParallelism)
                                       .Select(async partition =>
                                                   {
                                                       while (partition.MoveNext())
                                                       {
                                                           var index = partition.Current.Key;
                                                           var versionInfo = partition.Current.Value;

                                                           var elementMetadata = await GetElementMetadata(versionInfo.Key, versionInfo.VersionId);
                                                           objectVersions[index] = new ObjectVersion(
                                                               versionInfo.Key,
                                                               versionInfo.VersionId,
                                                               versionInfo.VersionIndex,
                                                               versionInfo.LastModified,
                                                               elementMetadata.AuthorInfo,
                                                               elementMetadata.ModifiedElements);
                                                       }
                                                   });
                await Task.WhenAll(tasks);

                versions.AddRange(objectVersions);

                return (!initialVersionIdReached && versionsResponse.IsTruncated, nextVersionIndex, versionsResponse.NextVersionIdMarker);
            }

            var result = await ListVersions(0, null);
            if (versions.Count == 0)
            {
                throw new ObjectNotFoundException($"Object '{id}' not found.");
            }

            while (result.IsTruncated)
            {
                result = await ListVersions(result.NextVersionIndex, result.NextVersionIdMarker);
            }

            var descriptors = new ModifiedObjectDescriptor[versions.Count];
            var descriptorsPartitioner = Partitioner.Create(versions);
            var descriptorsTasks =
                descriptorsPartitioner.GetOrderablePartitions(_degreeOfParallelism)
                                      .Select(
                                          async partition => await Task.Run(
                                                                 () =>
                                                                     {
                                                                         while (partition.MoveNext())
                                                                         {
                                                                             var index = (int)partition.Current.Key;
                                                                             var objectVersion = partition.Current.Value;

                                                                             descriptors[index] = new ModifiedObjectDescriptor(
                                                                                 objectVersion.Key.AsRootObjectId(),
                                                                                 objectVersion.VersionId,
                                                                                 versions[versions.Count - index - 1].VersionIndex,
                                                                                 objectVersion.LastModified,
                                                                                 objectVersion.AuthorInfo,
                                                                                 objectVersion.ModifiedElements);
                                                                         }
                                                                     }));
            await Task.WhenAll(descriptorsTasks);

            return descriptors;
        }

        public async Task<IReadOnlyCollection<VersionedObjectDescriptor<string>>> GetObjectLatestVersions(long id)
        {
            var versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, id + "/");
            return versionsResponse.Versions
                                   .Where(x => !x.IsDeleteMarker && x.IsLatest && !x.Key.EndsWith("/"))
                                   .Select(x => new VersionedObjectDescriptor<string>(x.Key, x.VersionId, x.LastModified))
                                   .ToArray();
        }

        public async Task<ObjectDescriptor> GetObjectDescriptor(long id, string versionId)
        {
            string objectVersionId;
            if (string.IsNullOrEmpty(versionId))
            {
                var objectVersions = await GetObjectLatestVersions(id);
                objectVersionId = objectVersions.Where(x => x.Id.EndsWith(Tokens.ObjectPostfix))
                                                .Select(x => x.VersionId)
                                                .SingleOrDefault();

                if (objectVersionId == null)
                {
                    throw new ObjectNotFoundException($"Object '{id}' not found.");
                }
            }
            else
            {
                objectVersionId = versionId;
            }

            (var persistenceDescriptor, var objectAuthorInfo, var objectLastModified) =
                await GetObjectFromS3<ObjectPersistenceDescriptor>(id.AsS3ObjectKey(Tokens.ObjectPostfix), objectVersionId);

            var elements = new IObjectElementDescriptor[persistenceDescriptor.Elements.Count];
            var tasks = persistenceDescriptor.Elements.Select(
                async (x, index) =>
                    {
                        (var elementPersistenceDescriptor, var _, var elementLastModified) =
                            await GetObjectFromS3<ObjectElementPersistenceDescriptor>(x.Id, x.VersionId);

                        var binaryElementValue = elementPersistenceDescriptor.Value as IBinaryElementValue;
                        if (binaryElementValue != null && !string.IsNullOrEmpty(binaryElementValue.Raw))
                        {
                            binaryElementValue.DownloadUri = new Uri(_fileStorageEndpoint, binaryElementValue.Raw);

                            if (binaryElementValue is IImageElementValue imageElementValue)
                            {
                                imageElementValue.PreviewUri = new Uri(_fileStorageEndpoint, imageElementValue.Raw);
                            }
                        }

                        elements[index] = new ObjectElementDescriptor
                            {
                                Id = x.Id.AsSubObjectId(),
                                VersionId = x.VersionId,
                                LastModified = elementLastModified,
                                Type = elementPersistenceDescriptor.Type,
                                TemplateCode = elementPersistenceDescriptor.TemplateCode,
                                Properties = elementPersistenceDescriptor.Properties,
                                Constraints = elementPersistenceDescriptor.Constraints,
                                Value = elementPersistenceDescriptor.Value
                            };
                    });
            await Task.WhenAll(tasks);

            var descriptor = new ObjectDescriptor
                                 {
                                     Id = id,
                                     VersionId = objectVersionId,
                                     LastModified = objectLastModified,
                                     TemplateId = persistenceDescriptor.TemplateId,
                                     TemplateVersionId = persistenceDescriptor.TemplateVersionId,
                                     Language = persistenceDescriptor.Language,
                                     Author = objectAuthorInfo.Author,
                                     AuthorLogin = objectAuthorInfo.AuthorLogin,
                                     AuthorName = objectAuthorInfo.AuthorName,
                                     Properties = persistenceDescriptor.Properties,
                                     Elements = elements
                                 };
            return descriptor;
        }

        public async Task<bool> IsObjectExists(long id)
        {
            var listResponse = await _amazonS3.ListObjectsV2Async(
                                   new ListObjectsV2Request
                                   {
                                       BucketName = _bucketName,
                                       MaxKeys = 1,
                                       Prefix = id.ToString() + "/" + Tokens.ObjectPostfix
                                   });
            return listResponse.S3Objects.Count != 0;
        }

        private async Task<(T, AuthorInfo, DateTime)> GetObjectFromS3<T>(string key, string versionId)
        {
            GetObjectResponse getObjectResponse;
            try
            {
                getObjectResponse = await _amazonS3.GetObjectAsync(_bucketName, key, versionId);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ObjectNotFoundException($"Object '{key}' with versionId '{versionId}' not found.");
            }

            var metadataWrapper = MetadataCollectionWrapper.For(getObjectResponse.Metadata);
            var author = metadataWrapper.Read<string>(MetadataElement.Author);
            var authorLogin = metadataWrapper.Read<string>(MetadataElement.AuthorLogin);
            var authorName = metadataWrapper.Read<string>(MetadataElement.AuthorName);

            string content;
            using (var reader = new StreamReader(getObjectResponse.ResponseStream, Encoding.UTF8))
            {
                content = reader.ReadToEnd();
            }

            var obj = JsonConvert.DeserializeObject<T>(content, SerializerSettings.Default);
            return (obj, new AuthorInfo(author, authorLogin, authorName), getObjectResponse.LastModified);
        }

        private struct ObjectVersion
        {
            public ObjectVersion(
                string id,
                string versionId,
                int versionIndex,
                DateTime lastModified,
                AuthorInfo authorInfo,
                IReadOnlyCollection<int> modifiedElements)
            {
                Key = id;
                VersionId = versionId;
                VersionIndex = versionIndex;
                LastModified = lastModified;
                AuthorInfo = authorInfo;
                ModifiedElements = modifiedElements;
            }

            public string Key { get; }
            public string VersionId { get; }
            public int VersionIndex { get; }
            public DateTime LastModified { get; }
            public AuthorInfo AuthorInfo { get; }
            public IReadOnlyCollection<int> ModifiedElements { get; }
        }
    }
}

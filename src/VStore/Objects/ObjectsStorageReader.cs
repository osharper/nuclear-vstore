using System;
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
            ObjectPersistenceDescriptor persistenceDescriptor = await GetObjectFromS3<ObjectPersistenceDescriptor>(id.AsS3ObjectKey(Tokens.ObjectPostfix), versionId);
            return await _templatesStorageReader.GetTemplateDescriptor(persistenceDescriptor.TemplateId, persistenceDescriptor.TemplateVersionId);
        }

        public async Task<IReadOnlyCollection<ModifiedObjectDescriptor>> GetAllObjectRootVersions(long id)
        {
            var versions = new List<ModifiedObjectDescriptor>();

            async Task<IReadOnlyCollection<int>> GetModifiedElements(string key, string versionId)
            {
                var metadataResponse = await _amazonS3.GetObjectMetadataAsync(_bucketName, key, versionId);

                var metadataWrapper = MetadataCollectionWrapper.For(metadataResponse.Metadata);
                var modifiedElements = metadataWrapper.Read<string>(MetadataElement.ModifiedElements);
                return string.IsNullOrEmpty(modifiedElements)
                           ? Array.Empty<int>()
                           : modifiedElements.Split(Tokens.ModifiedElementsDelimiter).Select(int.Parse).ToArray();
            }

            async Task<ListVersionsResponse> ListVersions(string nextVersionIdMarker)
            {
                var versionsResponse = await _amazonS3.ListVersionsAsync(new ListVersionsRequest
                    {
                        BucketName = _bucketName,
                        Prefix = id.AsS3ObjectKey(Tokens.ObjectPostfix),
                        VersionIdMarker = nextVersionIdMarker
                    });
                var versionInfos = versionsResponse.Versions.Where(x => !x.IsDeleteMarker)
                                                   .Select(x => new { x.Key, x.VersionId, x.LastModified })
                                                   .ToArray();

                var descriptors = new ModifiedObjectDescriptor[versionInfos.Length];
                var tasks = versionInfos.Select(
                    async (x, index) =>
                        {
                            var modifiedElements = await GetModifiedElements(x.Key, x.VersionId);
                            descriptors[index] = new ModifiedObjectDescriptor(x.Key.AsRootObjectId(),
                                                                              x.VersionId,
                                                                              x.LastModified,
                                                                              modifiedElements);
                        });
                await Task.WhenAll(tasks);

                versions.AddRange(descriptors);

                return versionsResponse;
            }

            var response = await ListVersions(null);
            if (versions.Count == 0)
            {
                throw new ObjectNotFoundException($"Object '{id}' not found.");
            }

            while (response.IsTruncated)
            {
                response = await ListVersions(response.NextVersionIdMarker);
            }

            return versions;
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

            var persistenceDescriptorWrapper = await GetObjectFromS3<ObjectPersistenceDescriptor>(id.AsS3ObjectKey(Tokens.ObjectPostfix), objectVersionId);
            var persistenceDescriptor = (ObjectPersistenceDescriptor)persistenceDescriptorWrapper;

            var elements = new IObjectElementDescriptor[persistenceDescriptor.Elements.Count];
            var tasks = persistenceDescriptor.Elements.Select(
                async (x, index) =>
                    {
                        var elementPersistenceDescriptorWrapper = await GetObjectFromS3<ObjectElementPersistenceDescriptor>(x.Id, x.VersionId);
                        var elementPersistenceDescriptor = (ObjectElementPersistenceDescriptor)elementPersistenceDescriptorWrapper;

                        var binaryElementValue = elementPersistenceDescriptor.Value as IBinaryElementValue;
                        if (binaryElementValue != null)
                        {
                            binaryElementValue.DownloadUri = new Uri(_fileStorageEndpoint, binaryElementValue.Raw);
                        }

                        if (binaryElementValue is IImageElementValue imageElementValue)
                        {
                            imageElementValue.PreviewUri = new Uri(_fileStorageEndpoint, imageElementValue.Raw);
                        }

                        elements[index] = new ObjectElementDescriptor
                            {
                                Id = x.Id.AsSubObjectId(),
                                VersionId = x.VersionId,
                                LastModified = elementPersistenceDescriptorWrapper.LastModified,
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
                                     LastModified = persistenceDescriptorWrapper.LastModified,
                                     TemplateId = persistenceDescriptor.TemplateId,
                                     TemplateVersionId = persistenceDescriptor.TemplateVersionId,
                                     Language = persistenceDescriptor.Language,
                                     Author = persistenceDescriptorWrapper.AuthorInfo.Author,
                                     AuthorLogin = persistenceDescriptorWrapper.AuthorInfo.AuthorLogin,
                                     AuthorName = persistenceDescriptorWrapper.AuthorInfo.AuthorName,
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

        private async Task<ObjectWrapper<T>> GetObjectFromS3<T>(string key, string versionId)
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
            return new ObjectWrapper<T>(obj, new AuthorInfo(author, authorLogin, authorName), getObjectResponse.LastModified);
        }

        private class ObjectWrapper<T>
        {
            private readonly T _object;

            public ObjectWrapper(T @object, AuthorInfo authorInfo, DateTime lastModified)
            {
                _object = @object;
                AuthorInfo = authorInfo;
                LastModified = lastModified;
            }

            public AuthorInfo AuthorInfo { get; }
            public DateTime LastModified { get; }

            public static implicit operator T(ObjectWrapper<T> wrapper)
            {
                return wrapper._object;
            }
        }
    }
}

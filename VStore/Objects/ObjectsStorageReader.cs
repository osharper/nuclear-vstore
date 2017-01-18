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

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;
using NuClear.VStore.Options;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

using S3ObjectVersion = NuClear.VStore.S3.S3ObjectVersion;

namespace NuClear.VStore.Objects
{
    public sealed class ObjectsStorageReader
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly TemplatesStorageReader _templatesStorageReader;
        private readonly string _bucketName;

        public ObjectsStorageReader(
            CephOptions cephOptions,
            IAmazonS3 amazonS3,
            TemplatesStorageReader templatesStorageReader)
        {
            _amazonS3 = amazonS3;
            _templatesStorageReader = templatesStorageReader;
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
                throw new ObjectNotFoundException($"Template for the object '{id}' not found.");
            }

            if (listResponse.S3Objects.Count > 1)
            {
                throw new ObjectInconsistentException(id, $"More than one template found for the object '{id}'.");
            }

            var templateId = listResponse.S3Objects[0].Key.AsObjectId();

            var metadataResponse = await _amazonS3.GetObjectMetadataAsync(_bucketName, id.AsS3ObjectKey(Tokens.ObjectPostfix), versionId);
            var metadataWrapper = MetadataCollectionWrapper.For(metadataResponse.Metadata);
            var templateVersionId = metadataWrapper.Read<string>(MetadataElement.TemplateVersionId);

            if (string.IsNullOrEmpty(templateVersionId))
            {
                throw new ObjectInconsistentException(id, "Template version cannot be determined.");
            }

            return await _templatesStorageReader.GetTemplateDescriptor(templateId, templateVersionId);
        }

        public async Task<IReadOnlyCollection<S3ObjectVersion>> GetObjectLatestVersions(long id)
        {
            var versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, id + "/");
            return versionsResponse.Versions.FindAll(x => x.IsLatest)
                                   .Where(x => !x.Key.EndsWith("/"))
                                   .Select(x => new S3ObjectVersion { Key = x.Key, VersionId = x.VersionId, LastModified = x.LastModified })
                                   .ToArray();
        }

        public async Task<ObjectDescriptor> GetObjectDescriptor(long id, string versionId)
        {
            string objectVersionId;
            if (string.IsNullOrEmpty(versionId))
            {
                var objectVersions = await GetObjectLatestVersions(id);
                objectVersionId = objectVersions.Where(x => x.Key.EndsWith(Tokens.ObjectPostfix))
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
            var persistenceDescriptor = persistenceDescriptorWrapper.Object;

            var elements = new ConcurrentBag<IObjectElementDescriptor>();
            Parallel.ForEach(
                persistenceDescriptor.Elements,
                objectVersion =>
                    {
                        var elementDescriptorWrapper = GetObjectFromS3<ObjectElementDescriptor>(objectVersion.Key, objectVersion.VersionId).Result;
                        var elementDescriptor = elementDescriptorWrapper.Object;

                        elementDescriptor.Id = objectVersion.Key.AsObjectId();
                        elementDescriptor.VersionId = objectVersion.VersionId;
                        elementDescriptor.LastModified = elementDescriptorWrapper.LastModified;

                        elements.Add(elementDescriptor);
                    });

            var descriptor = new ObjectDescriptor
                                 {
                                     Id = id,
                                     VersionId = objectVersionId,
                                     LastModified = persistenceDescriptorWrapper.LastModified,
                                     TemplateId = persistenceDescriptor.TemplateId,
                                     TemplateVersionId = persistenceDescriptor.TemplateVersionId,
                                     Language = persistenceDescriptor.Language,
                                     Author = persistenceDescriptorWrapper.Author,
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
                                       Prefix = id.ToString()
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
            catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ObjectNotFoundException($"Object '{key}' with versionId '{versionId}' not found.");
            }

            var metadataWrapper = MetadataCollectionWrapper.For(getObjectResponse.Metadata);
            var author = metadataWrapper.Read<string>(MetadataElement.Author);

            string content;
            using (var reader = new StreamReader(getObjectResponse.ResponseStream, Encoding.UTF8))
            {
                content = reader.ReadToEnd();
            }

            var obj = JsonConvert.DeserializeObject<T>(content, SerializerSettings.Default);
            return new ObjectWrapper<T>(obj, author, getObjectResponse.LastModified);
        }

        private class ObjectWrapper<T>
        {
            public ObjectWrapper(T @object, string author, DateTime lastModified)
            {
                Object = @object;
                Author = author;
                LastModified = lastModified;
            }

            public T Object { get; }
            public string Author { get; }
            public DateTime LastModified { get; }
        }
    }
}
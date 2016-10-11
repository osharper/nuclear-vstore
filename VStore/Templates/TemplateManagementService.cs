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
using NuClear.VStore.Locks;
using NuClear.VStore.Options;
using NuClear.VStore.S3;

namespace NuClear.VStore.Templates
{
    public sealed class TemplateManagementService
    {
        private static readonly IReadOnlyCollection<FileFormat> ImageFileFormats =
            new[] { FileFormat.Bmp, FileFormat.Gif, FileFormat.Png };

        private static readonly IReadOnlyCollection<FileFormat> ArticleFileFormats =
            new[] { FileFormat.Chm };

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
                    new ArticleElementDescriptor(),
                    new FasCommantElementDescriptor(),
                    new DateElementDescriptor(),
                    new LinkElementDescriptor()
                };
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

            var descriptors = JsonConvert.DeserializeObject<IEnumerable<IElementDescriptor>>(content, new TemplateDescriptorJsonConverter());
            foreach (var elementDescriptor in descriptors)
            {
                descriptor.ElementDescriptors.Add(elementDescriptor);
            }

            return descriptor;
        }

        public async Task<string> CreateTemplate(ITemplateDescriptor templateDescriptor)
        {
            if (templateDescriptor.Id == Guid.Empty)
            {
                throw new InvalidOperationException($"Template Id must be set to the value different from '{templateDescriptor.Id}'");
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
                if (listResponse.S3Objects.Count != 0)
                {
                    throw new InvalidOperationException($"Template with Id '{templateDescriptor.Id}' already exists");
                }

                await PutTemplate(id, templateDescriptor.Name, templateDescriptor.IsMandatory, templateDescriptor.ElementDescriptors);

                // ceph does not return version-id response header, so we need to do another request to get version
                var versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, id);
                return versionsResponse.Versions[0].VersionId;
            }
        }

        public async Task<string> ModifyTemplate(IModifiableTemplateDescriptor templateDescriptor)
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

                await PutTemplate(id, templateDescriptor.Name, templateDescriptor.IsMandatory, templateDescriptor.ElementDescriptors);

                // ceph does not return version-id response header, so we need to do another request to get version
                versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, id);
                return versionsResponse.Versions.Find(x => x.IsLatest).VersionId;
            }
        }

        private static void VerifyDescriptorsConsistency(string templateId, IEnumerable<IElementDescriptor> elementDescriptors)
        {
            foreach (var elementDescriptor in elementDescriptors)
            {
                ImageElementDescriptor imageElementDescriptor;
                ArticleElementDescriptor articleElementDescriptor;

                if ((imageElementDescriptor = elementDescriptor as ImageElementDescriptor) != null)
                {
                    if (imageElementDescriptor.SupportedFileFormats.Any(x => !ImageFileFormats.Contains(x)))
                    {
                        throw new TemplateInconsistentException(
                            templateId,
                            $"Supported file formats for images are: {string.Join(",", ImageFileFormats)}");
                    }

                    if (imageElementDescriptor.ImageSize == ImageSize.Empty)
                    {
                        throw new TemplateInconsistentException(
                            templateId,
                            $"Image size must be set to the value different than: {ImageSize.Empty}");
                    }
                }
                else if ((articleElementDescriptor = elementDescriptor as ArticleElementDescriptor) != null)
                {
                    if (articleElementDescriptor.SupportedFileFormats.Any(x => !ArticleFileFormats.Contains(x)))
                    {
                        throw new TemplateInconsistentException(
                            templateId,
                            $"Supported file formats for articles are: {string.Join(",", ImageFileFormats)}");
                    }
                }
            }
        }

        private async Task PutTemplate(string id, string name, bool isMandatory, IEnumerable<IElementDescriptor> elementDescriptors)
        {
            VerifyDescriptorsConsistency(id, elementDescriptors);

            var content = JsonConvert.SerializeObject(elementDescriptors, SerializerSettings.Default);
            var putRequest = new PutObjectRequest
                {
                    Key = id,
                    BucketName = _bucketName,
                    ContentType = "application/javascript",
                    ContentBody = content,
                    CannedACL = S3CannedACL.PublicRead,
                };

            var metadataWrapper = MetadataCollectionWrapper.For(putRequest.Metadata);
            metadataWrapper.Write(MetadataElement.Name, name);
            metadataWrapper.Write(MetadataElement.IsMandatory, isMandatory);

            await _amazonS3.PutObjectAsync(putRequest);
        }
    }
}
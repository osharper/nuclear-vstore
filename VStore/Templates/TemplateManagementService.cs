using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly TemplateStorageReader _templateStorageReader;
        private readonly LockSessionFactory _lockSessionFactory;
        private readonly string _bucketName;

        public TemplateManagementService(
            CephOptions cephOptions,
            IAmazonS3 amazonS3,
            TemplateStorageReader templateStorageReader,
            LockSessionFactory lockSessionFactory)
        {
            _amazonS3 = amazonS3;
            _templateStorageReader = templateStorageReader;
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

        public async Task<string> CreateTemplate(ITemplateDescriptor templateDescriptor)
        {
            if (templateDescriptor.Id == Guid.Empty)
            {
                throw new ArgumentException($"Template Id must be set to the value different from '{templateDescriptor.Id}'", nameof(templateDescriptor.Id));
            }

            using (_lockSessionFactory.CreateLockSession(templateDescriptor.Id))
            {
                if (await _templateStorageReader.IsTemplateExists(templateDescriptor.Id))
                {
                    throw new InvalidOperationException($"Template '{templateDescriptor.Id}' already exists");
                }

                await PutTemplate(templateDescriptor.Id, templateDescriptor.Name, templateDescriptor.IsMandatory, templateDescriptor.ElementDescriptors);

                // ceph does not return version-id response header, so we need to do another request to get version
                return await _templateStorageReader.GetTemplateLatestVersion(templateDescriptor.Id);
            }
        }

        public async Task<string> ModifyTemplate(IVersionedTemplateDescriptor templateDescriptor)
        {
            if (templateDescriptor.Id == Guid.Empty)
            {
                throw new ArgumentException("Template Id must be set", nameof(templateDescriptor.Id));
            }

            if (string.IsNullOrEmpty(templateDescriptor.VersionId))
            {
                throw new ArgumentException("VersionId must be set", nameof(templateDescriptor.VersionId));
            }

            using (_lockSessionFactory.CreateLockSession(templateDescriptor.Id))
            {
                if (!await _templateStorageReader.IsTemplateExists(templateDescriptor.Id) || string.IsNullOrEmpty(templateDescriptor.VersionId))
                {
                    throw new InvalidOperationException($"Template '{templateDescriptor.Id}' does not exist");
                }

                var latestVersionId = await _templateStorageReader.GetTemplateLatestVersion(templateDescriptor.Id);
                if (!templateDescriptor.VersionId.Equals(latestVersionId, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException($"Template '{templateDescriptor.Id}' cannot be modified. " +
                                                        $"Reason: version '{templateDescriptor.VersionId}' has been overwritten. " +
                                                        $"Latest versionId is '{latestVersionId}'");
                }

                await PutTemplate(templateDescriptor.Id, templateDescriptor.Name, templateDescriptor.IsMandatory, templateDescriptor.ElementDescriptors);

                // ceph does not return version-id response header, so we need to do another request to get version
                return await _templateStorageReader.GetTemplateLatestVersion(templateDescriptor.Id);
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

        private async Task PutTemplate(Guid id, string name, bool isMandatory, IReadOnlyCollection<IElementDescriptor> elementDescriptors)
        {
            var templateId = id.ToString();

            VerifyDescriptorsConsistency(templateId, elementDescriptors);

            var content = JsonConvert.SerializeObject(elementDescriptors, SerializerSettings.Default);
            var putRequest = new PutObjectRequest
                {
                    Key = templateId,
                    BucketName = _bucketName,
                    ContentType = ContentType.Json,
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
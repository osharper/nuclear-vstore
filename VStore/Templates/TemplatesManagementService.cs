using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;
using NuClear.VStore.Locks;
using NuClear.VStore.Options;
using NuClear.VStore.S3;

namespace NuClear.VStore.Templates
{
    public sealed class TemplatesManagementService
    {
        private static readonly IReadOnlyCollection<FileFormat> ImageFileFormats =
            new[] { FileFormat.Bmp, FileFormat.Gif, FileFormat.Png };

        private static readonly IReadOnlyCollection<FileFormat> ArticleFileFormats =
            new[] { FileFormat.Chm };

        private readonly IAmazonS3 _amazonS3;
        private readonly TemplatesStorageReader _templatesStorageReader;
        private readonly LockSessionFactory _lockSessionFactory;
        private readonly string _bucketName;

        public TemplatesManagementService(
            CephOptions cephOptions,
            IAmazonS3 amazonS3,
            TemplatesStorageReader templatesStorageReader,
            LockSessionFactory lockSessionFactory)
        {
            _amazonS3 = amazonS3;
            _templatesStorageReader = templatesStorageReader;
            _lockSessionFactory = lockSessionFactory;
            _bucketName = cephOptions.TemplatesBucketName;
        }

        public IReadOnlyCollection<IElementDescriptor> GetAvailableElementDescriptors()
        {
            return new IElementDescriptor[]
                       {
                           new ElementDescriptor(ElementDescriptorType.Text, 1, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new TextElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.Image, 2, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new ImageElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.Article, 3, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new ArticleElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.FasComment, 4, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new TextElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.Date, 5, new JObject(), null),
                           new ElementDescriptor(ElementDescriptorType.Link, 6, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new TextElementConstraints()) }))
                       };
        }

        public async Task<string> CreateTemplate(long id, string author, ITemplateDescriptor templateDescriptor)
        {
            if (id == 0)
            {
                throw new ArgumentException("Template Id must be set", nameof(id));
            }

            using (_lockSessionFactory.CreateLockSession(id))
            {
                if (await _templatesStorageReader.IsTemplateExists(id))
                {
                    throw new InvalidOperationException($"Template '{id}' already exists");
                }

                await PutTemplate(id, author, templateDescriptor);

                // ceph does not return version-id response header, so we need to do another request to get version
                return await _templatesStorageReader.GetTemplateLatestVersion(id);
            }
        }

        public async Task<string> ModifyTemplate(long id, string versionId, string author, ITemplateDescriptor templateDescriptor)
        {
            if (id == 0)
            {
                throw new ArgumentException("Template Id must be set", nameof(id));
            }

            if (string.IsNullOrEmpty(versionId))
            {
                throw new ArgumentException("VersionId must be set", nameof(versionId));
            }

            using (_lockSessionFactory.CreateLockSession(id))
            {
                if (!await _templatesStorageReader.IsTemplateExists(id))
                {
                    throw new InvalidOperationException($"Template '{id}' does not exist");
                }

                var latestVersionId = await _templatesStorageReader.GetTemplateLatestVersion(id);
                if (!versionId.Equals(latestVersionId, StringComparison.Ordinal))
                {
                    throw new ConcurrencyException(id.ToString(), versionId, latestVersionId);
                }

                await PutTemplate(id, author, templateDescriptor);

                // ceph does not return version-id response header, so we need to do another request to get version
                return await _templatesStorageReader.GetTemplateLatestVersion(id);
            }
        }

        public void VerifyElementDescriptorsConsistency(long? templateId, IEnumerable<IElementDescriptor> elementDescriptors)
        {
            Parallel.ForEach(
                elementDescriptors,
                elementDescriptor =>
                    {
                        foreach (var constraints in elementDescriptor.Constraints)
                        {
                            TextElementConstraints textElementConstraints;
                            ImageElementConstraints imageElementConstraints;
                            ArticleElementConstraints articleElementConstraints;
                            if ((textElementConstraints = constraints.ElementConstraints as TextElementConstraints) != null)
                            {
                                VerifyTextConstraints(templateId, textElementConstraints, elementDescriptor);
                            }
                            else if ((imageElementConstraints = constraints.ElementConstraints as ImageElementConstraints) != null)
                            {
                                VerifyImageConstraints(templateId, imageElementConstraints);
                            }
                            else if ((articleElementConstraints = constraints.ElementConstraints as ArticleElementConstraints) != null)
                            {
                                VerifyArticleConstraints(templateId, articleElementConstraints);
                            }
                        }
                    });
        }

        private static void VerifyArticleConstraints(long? templateId, ArticleElementConstraints articleElementConstraints)
        {
            if (articleElementConstraints.SupportedFileFormats == null)
            {
                throw new TemplateInconsistentException(
                          templateId,
                          "Supported file formats constraints cannot be null");
            }

            if (!articleElementConstraints.SupportedFileFormats.Any())
            {
                throw new TemplateInconsistentException(
                          templateId,
                          "Supported file formats constraints must be set");
            }

            if (articleElementConstraints.SupportedFileFormats.Any(x => !ArticleFileFormats.Contains(x)))
            {
                throw new TemplateInconsistentException(
                          templateId,
                          $"Supported file formats for articles are: {string.Join(",", ArticleFileFormats)}");
            }

            if (articleElementConstraints.MaxFilenameLength <= 0)
            {
                throw new TemplateInconsistentException(templateId, "MaxFilenameLength must be positive");
            }

            if (articleElementConstraints.MaxSize <= 0)
            {
                throw new TemplateInconsistentException(templateId, "MaxSize must be positive");
            }
        }

        private static void VerifyImageConstraints(long? templateId, ImageElementConstraints imageElementConstraints)
        {
            if (imageElementConstraints.SupportedFileFormats == null)
            {
                throw new TemplateInconsistentException(
                          templateId,
                          "Supported file formats constraints cannot be null");
            }

            if (!imageElementConstraints.SupportedFileFormats.Any())
            {
                throw new TemplateInconsistentException(
                          templateId,
                          "Supported file formats constraints must be set");
            }

            if (imageElementConstraints.SupportedFileFormats.Any(x => !ImageFileFormats.Contains(x)))
            {
                throw new TemplateInconsistentException(
                          templateId,
                          $"Supported file formats for images are: {string.Join(",", ImageFileFormats)}");
            }

            if (imageElementConstraints.SupportedImageSizes == null)
            {
                throw new TemplateInconsistentException(
                          templateId,
                          "Supported image sizes constraints must be set");
            }

            if (imageElementConstraints.SupportedImageSizes.Contains(ImageSize.Empty))
            {
                throw new TemplateInconsistentException(
                          templateId,
                          $"Supported image sizes constraints cannot contain '{ImageSize.Empty}' value");
            }

            if (imageElementConstraints.MaxFilenameLength <= 0)
            {
                throw new TemplateInconsistentException(templateId, "MaxFilenameLength must be positive");
            }

            if (imageElementConstraints.MaxSize <= 0)
            {
                throw new TemplateInconsistentException(templateId, "MaxSize must be positive");
            }
        }

        private static void VerifyTextConstraints(long? templateId, TextElementConstraints textElementConstraints, IElementDescriptor elementDescriptor)
        {
            if (textElementConstraints.MaxSymbols < textElementConstraints.MaxSymbolsPerWord)
            {
                throw new TemplateInconsistentException(
                          templateId,
                          "MaxSymbols must be equal or greater than MaxSymbolsPerWord");
            }

            if (elementDescriptor.Type != ElementDescriptorType.Text && textElementConstraints.IsFormatted)
            {
                throw new TemplateInconsistentException(templateId, "Only text element can be formatted");
            }

            if (textElementConstraints.MaxSymbols <= 0)
            {
                throw new TemplateInconsistentException(templateId, "MaxSymbols must be positive");
            }

            if (textElementConstraints.MaxSymbolsPerWord <= 0)
            {
                throw new TemplateInconsistentException(templateId, "MaxSymbolsPerWord must be positive");
            }

            if (textElementConstraints.MaxLines <= 0)
            {
                throw new TemplateInconsistentException(templateId, "MaxLines must be positive");
            }
        }

        private async Task PutTemplate(long id, string author, ITemplateDescriptor templateDescriptor)
        {
            VerifyElementDescriptorsConsistency(id, templateDescriptor.Elements);

            var putRequest = new PutObjectRequest
                {
                    Key = id.ToString(),
                    BucketName = _bucketName,
                    ContentType = ContentType.Json,
                    ContentBody = JsonConvert.SerializeObject(templateDescriptor, SerializerSettings.Default),
                    CannedACL = S3CannedACL.PublicRead,
                };
            var metadataWrapper = MetadataCollectionWrapper.For(putRequest.Metadata);
            metadataWrapper.Write(MetadataElement.Title, author);

            await _amazonS3.PutObjectAsync(putRequest);
        }
    }
}
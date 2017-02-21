using System;
using System.Collections.Concurrent;
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
using NuClear.VStore.Objects;
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
                           new ElementDescriptor(ElementDescriptorType.Text, 1, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new FormattedTextElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.Text, 2, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new PlainTextElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.Image, 3, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new ImageElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.Article, 4, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new ArticleElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.FasComment, 5, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new PlainTextElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.Date, 6, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new DateElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.Link, 7, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new LinkElementConstraints()) }))
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
                    throw new ObjectAlreadyExistsException(id);
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
                    throw new ObjectNotFoundException($"Template '{id}' does not exist");
                }

                var latestVersionId = await _templatesStorageReader.GetTemplateLatestVersion(id);
                if (!versionId.Equals(latestVersionId, StringComparison.Ordinal))
                {
                    throw new ConcurrencyException(id, versionId, latestVersionId);
                }

                await PutTemplate(id, author, templateDescriptor);

                // ceph does not return version-id response header, so we need to do another request to get version
                return await _templatesStorageReader.GetTemplateLatestVersion(id);
            }
        }

        public void VerifyElementDescriptorsConsistency(IEnumerable<IElementDescriptor> elementDescriptors)
        {
            var codes = new ConcurrentDictionary<int, bool>();
            Parallel.ForEach(
                elementDescriptors,
                elementDescriptor =>
                    {
                        if (!codes.TryAdd(elementDescriptor.TemplateCode, true))
                        {
                            throw new TemplateValidationException(elementDescriptor.TemplateCode, TemplateElementValidationErrors.NonUniqueTemplateCode);
                        }

                        foreach (var constraints in elementDescriptor.Constraints)
                        {
                            TextElementConstraints textElementConstraints;
                            ImageElementConstraints imageElementConstraints;
                            ArticleElementConstraints articleElementConstraints;
                            if ((textElementConstraints = constraints.ElementConstraints as TextElementConstraints) != null)
                            {
                                VerifyTextConstraints(elementDescriptor.TemplateCode, textElementConstraints, elementDescriptor);
                            }
                            else if ((imageElementConstraints = constraints.ElementConstraints as ImageElementConstraints) != null)
                            {
                                VerifyImageConstraints(elementDescriptor.TemplateCode, imageElementConstraints);
                            }
                            else if ((articleElementConstraints = constraints.ElementConstraints as ArticleElementConstraints) != null)
                            {
                                VerifyArticleConstraints(elementDescriptor.TemplateCode, articleElementConstraints);
                            }
                        }
                    });
        }

        // ReSharper disable once UnusedParameter.Local
        private static void VerifyBinaryConstraints(int templateCode, IBinaryElementConstraints binaryElementConstraints)
        {
            if (binaryElementConstraints.SupportedFileFormats == null)
            {
                throw new TemplateValidationException(
                    templateCode,
                    TemplateElementValidationErrors.EmptySupportedFileFormats);
            }

            if (!binaryElementConstraints.SupportedFileFormats.Any())
            {
                throw new TemplateValidationException(
                    templateCode,
                    TemplateElementValidationErrors.EmptySupportedFileFormats);
            }

            if (binaryElementConstraints.MaxFilenameLength <= 0)
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.NegativeMaxFilenameLength);
            }

            if (binaryElementConstraints.MaxSize <= 0)
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.NegativeMaxSize);
            }
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private static void VerifyArticleConstraints(int templateCode, ArticleElementConstraints articleElementConstraints)
        {
            VerifyBinaryConstraints(templateCode, articleElementConstraints);

            if (articleElementConstraints.SupportedFileFormats.Any(x => !ArticleFileFormats.Contains(x)))
            {
                throw new TemplateValidationException(
                          templateCode,
                          TemplateElementValidationErrors.UnsupportedArticleFileFormat);
            }
        }

        private static void VerifyImageConstraints(int templateCode, ImageElementConstraints imageElementConstraints)
        {
            VerifyBinaryConstraints(templateCode, imageElementConstraints);

            if (imageElementConstraints.SupportedFileFormats.Any(x => !ImageFileFormats.Contains(x)))
            {
                throw new TemplateValidationException(
                          templateCode,
                          TemplateElementValidationErrors.UnsupportedImageFileFormat);
            }

            if (imageElementConstraints.SupportedImageSizes == null)
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.EmptySupportedImageSizes);
            }

            if (imageElementConstraints.SupportedImageSizes.Contains(ImageSize.Empty))
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.InvalidImageSize);
            }

            if (imageElementConstraints.SupportedImageSizes.Any(x => x.Height < 0 || x.Width < 0))
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.NegativeImageSizeDimension);
            }
        }

        private static void VerifyTextConstraints(int templateCode, TextElementConstraints textElementConstraints, IElementDescriptor elementDescriptor)
        {
            if (textElementConstraints.MaxSymbols < textElementConstraints.MaxSymbolsPerWord)
            {
                throw new TemplateValidationException(
                          templateCode,
                          TemplateElementValidationErrors.InvalidMaxSymblosPerWord);
            }

            if (textElementConstraints.MaxSymbols <= 0)
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.NegativeMaxSymbols);
            }

            if (textElementConstraints.MaxSymbolsPerWord <= 0)
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.NegativeMaxSymbolsPerWord);
            }

            if (textElementConstraints.MaxLines <= 0)
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.NegativeMaxLines);
            }
        }

        private async Task PutTemplate(long id, string author, ITemplateDescriptor templateDescriptor)
        {
            VerifyElementDescriptorsConsistency(templateDescriptor.Elements);

            var putRequest = new PutObjectRequest
                {
                    Key = id.ToString(),
                    BucketName = _bucketName,
                    ContentType = ContentType.Json,
                    ContentBody = JsonConvert.SerializeObject(templateDescriptor, SerializerSettings.Default),
                    CannedACL = S3CannedACL.PublicRead,
                };
            var metadataWrapper = MetadataCollectionWrapper.For(putRequest.Metadata);
            metadataWrapper.Write(MetadataElement.Author, author);

            await _amazonS3.PutObjectAsync(putRequest);
        }
    }
}

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
using NuClear.VStore.Http;
using NuClear.VStore.Json;
using NuClear.VStore.Locks;
using NuClear.VStore.Objects;
using NuClear.VStore.Options;
using NuClear.VStore.S3;

namespace NuClear.VStore.Templates
{
    public sealed class TemplatesManagementService
    {
        private static readonly IReadOnlyCollection<FileFormat> BitmapImageFileFormats =
            new[] { FileFormat.Bmp, FileFormat.Gif, FileFormat.Png };

        private static readonly IReadOnlyCollection<FileFormat> VectorImageFileFormats =
            new[] { FileFormat.Pdf, FileFormat.Svg };

        private static readonly IReadOnlyCollection<FileFormat> ArticleFileFormats =
            new[] { FileFormat.Chm };

        private readonly IAmazonS3Proxy _amazonS3;
        private readonly TemplatesStorageReader _templatesStorageReader;
        private readonly LockSessionFactory _lockSessionFactory;
        private readonly string _bucketName;
        private readonly long _maxBinarySize;

        public TemplatesManagementService(
            VStoreOptions vstoreOptions,
            CephOptions cephOptions,
            IAmazonS3Proxy amazonS3,
            TemplatesStorageReader templatesStorageReader,
            LockSessionFactory lockSessionFactory)
        {
            _amazonS3 = amazonS3;
            _templatesStorageReader = templatesStorageReader;
            _lockSessionFactory = lockSessionFactory;
            _bucketName = cephOptions.TemplatesBucketName;
            _maxBinarySize = vstoreOptions.MaxBinarySize;
        }

        public IReadOnlyCollection<IElementDescriptor> GetAvailableElementDescriptors()
        {
            return new IElementDescriptor[]
                       {
                           new ElementDescriptor(ElementDescriptorType.PlainText, 1, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new PlainTextElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.FormattedText, 2, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new FormattedTextElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.BitmapImage, 3, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new BitmapImageElementConstraints { SupportedFileFormats = BitmapImageFileFormats }) })),
                           new ElementDescriptor(ElementDescriptorType.VectorImage, 4, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new VectorImageElementConstraints { SupportedFileFormats = VectorImageFileFormats }) })),
                           new ElementDescriptor(ElementDescriptorType.Article, 5, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new ArticleElementConstraints { SupportedFileFormats = ArticleFileFormats }) })),
                           new ElementDescriptor(ElementDescriptorType.FasComment, 6, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new PlainTextElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.Date, 7, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new DateElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.Link, 8, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new LinkElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.Phone, 9, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new PhoneElementConstraints()) })),
                           new ElementDescriptor(ElementDescriptorType.VideoLink, 10, new JObject(), new ConstraintSet(new[] { new ConstraintSetItem(Language.Unspecified, new VideoLinkElementConstraints()) }))
                       };
        }

        public async Task<string> CreateTemplate(long id, AuthorInfo authorInfo, ITemplateDescriptor templateDescriptor)
        {
            if (id == 0)
            {
                throw new ArgumentException("Template Id must be set", nameof(id));
            }

            LockSession lockSession = null;
            try
            {
                lockSession = await _lockSessionFactory.CreateLockSessionAsync(id);

                if (await _templatesStorageReader.IsTemplateExists(id))
                {
                    throw new ObjectAlreadyExistsException(id);
                }

                await PutTemplate(id, authorInfo, templateDescriptor);

                // ceph does not return version-id response header, so we need to do another request to get version
                return await _templatesStorageReader.GetTemplateLatestVersion(id);
            }
            finally
            {
                if (lockSession != null)
                {
                    await lockSession.ReleaseAsync();
                }
            }
        }

        public async Task<string> ModifyTemplate(long id, string versionId, AuthorInfo authorInfo, ITemplateDescriptor templateDescriptor)
        {
            if (id == 0)
            {
                throw new ArgumentException("Template Id must be set", nameof(id));
            }

            if (string.IsNullOrEmpty(versionId))
            {
                throw new ArgumentException("VersionId must be set", nameof(versionId));
            }

            LockSession lockSession = null;
            try
            {
                lockSession = await _lockSessionFactory.CreateLockSessionAsync(id);

                if (!await _templatesStorageReader.IsTemplateExists(id))
                {
                    throw new ObjectNotFoundException($"Template '{id}' does not exist");
                }

                var latestVersionId = await _templatesStorageReader.GetTemplateLatestVersion(id);
                if (!versionId.Equals(latestVersionId, StringComparison.Ordinal))
                {
                    throw new ConcurrencyException(id, versionId, latestVersionId);
                }

                await PutTemplate(id, authorInfo, templateDescriptor);

                // ceph does not return version-id response header, so we need to do another request to get version
                return await _templatesStorageReader.GetTemplateLatestVersion(id);
            }
            finally
            {
                if (lockSession != null)
                {
                    await lockSession.ReleaseAsync();
                }
            }
        }

        public async Task VerifyElementDescriptorsConsistency(IEnumerable<IElementDescriptor> elementDescriptors)
        {
            var codes = new ConcurrentDictionary<int, bool>();
            var tasks = elementDescriptors.Select(
                async x => await Task.Run(
                               () =>
                                   {
                                       if (!codes.TryAdd(x.TemplateCode, true))
                                       {
                                           throw new TemplateValidationException(x.TemplateCode, TemplateElementValidationErrors.NonUniqueTemplateCode);
                                       }

                                       foreach (var constraints in x.Constraints)
                                       {
                                           switch (constraints.ElementConstraints)
                                           {
                                               case TextElementConstraints textElementConstraints:
                                                   VerifyTextConstraints(x.TemplateCode, textElementConstraints);
                                                   break;
                                               case BitmapImageElementConstraints imageElementConstraints:
                                                   VerifyBitmapImageConstraints(x.TemplateCode, imageElementConstraints);
                                                   break;
                                               case VectorImageElementConstraints vectorImageElementConstraints:
                                                   VerifyVectorImageConstraints(x.TemplateCode, vectorImageElementConstraints);
                                                   break;
                                               case ArticleElementConstraints articleElementConstraints:
                                                   VerifyArticleConstraints(x.TemplateCode, articleElementConstraints);
                                                   break;
                                               case DateElementConstraints _:
                                               case PhoneElementConstraints _:
                                               case VideoLinkElementConstraints _:
                                                   break;
                                               default:
                                                   throw new ArgumentOutOfRangeException(nameof(constraints.ElementConstraints), constraints.ElementConstraints, "Unsupported element contraints");
                                           }
                                       }
                                   }));
            await Task.WhenAll(tasks);
        }

        // ReSharper disable once UnusedParameter.Local
        private void VerifyBinaryConstraints(int templateCode, IBinaryElementConstraints binaryElementConstraints)
        {
            if (binaryElementConstraints.SupportedFileFormats == null)
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.MissingSupportedFileFormats);
            }

            if (!binaryElementConstraints.SupportedFileFormats.Any())
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.EmptySupportedFileFormats);
            }

            if (binaryElementConstraints.MaxFilenameLength <= 0)
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.NegativeMaxFilenameLength);
            }

            if (binaryElementConstraints.MaxSize <= 0)
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.NegativeMaxSize);
            }

            if (binaryElementConstraints.MaxSize > _maxBinarySize)
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.MaxSizeLimitExceeded);
            }
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private void VerifyArticleConstraints(int templateCode, ArticleElementConstraints articleElementConstraints)
        {
            VerifyBinaryConstraints(templateCode, articleElementConstraints);

            if (articleElementConstraints.SupportedFileFormats.Any(x => !ArticleFileFormats.Contains(x)))
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.UnsupportedArticleFileFormat);
            }
        }

        // ReSharper disable once SuggestBaseTypeForParameter
        private void VerifyVectorImageConstraints(int templateCode, VectorImageElementConstraints vectorImageConstraints)
        {
            VerifyBinaryConstraints(templateCode, vectorImageConstraints);

            if (vectorImageConstraints.SupportedFileFormats.Any(x => !VectorImageFileFormats.Contains(x)))
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.UnsupportedImageFileFormat);
            }
        }

        private void VerifyBitmapImageConstraints(int templateCode, BitmapImageElementConstraints imageElementConstraints)
        {
            VerifyBinaryConstraints(templateCode, imageElementConstraints);

            if (imageElementConstraints.SupportedFileFormats.Any(x => !BitmapImageFileFormats.Contains(x)))
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.UnsupportedImageFileFormat);
            }

            if (imageElementConstraints.SupportedImageSizes == null)
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.MissingSupportedImageSizes);
            }

            if (!imageElementConstraints.SupportedImageSizes.Any())
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

        private static void VerifyTextConstraints(int templateCode, TextElementConstraints textElementConstraints)
        {
            if (textElementConstraints.MaxSymbols < textElementConstraints.MaxSymbolsPerWord)
            {
                throw new TemplateValidationException(templateCode, TemplateElementValidationErrors.InvalidMaxSymbolsPerWord);
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

        private async Task PutTemplate(long id, AuthorInfo authorInfo, ITemplateDescriptor templateDescriptor)
        {
            await VerifyElementDescriptorsConsistency(templateDescriptor.Elements);

            var putRequest = new PutObjectRequest
                {
                    Key = id.ToString(),
                    BucketName = _bucketName,
                    ContentType = ContentType.Json,
                    ContentBody = JsonConvert.SerializeObject(templateDescriptor, SerializerSettings.Default),
                    CannedACL = S3CannedACL.PublicRead,
                };
            var metadataWrapper = MetadataCollectionWrapper.For(putRequest.Metadata);
            metadataWrapper.Write(MetadataElement.Author, authorInfo.Author);
            metadataWrapper.Write(MetadataElement.AuthorLogin, authorInfo.AuthorLogin);
            metadataWrapper.Write(MetadataElement.AuthorName, authorInfo.AuthorName);

            await _amazonS3.PutObjectAsync(putRequest);
        }
    }
}

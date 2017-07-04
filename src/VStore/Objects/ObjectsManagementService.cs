using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Objects.Persistence;
using NuClear.VStore.Descriptors.Sessions;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Http;
using NuClear.VStore.Json;
using NuClear.VStore.Locks;
using NuClear.VStore.Objects.ContentPreprocessing;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.Errors;
using NuClear.VStore.Options;
using NuClear.VStore.S3;
using NuClear.VStore.Sessions;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Objects
{
    public sealed class ObjectsManagementService
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly TemplatesStorageReader _templatesStorageReader;
        private readonly ObjectsStorageReader _objectsStorageReader;
        private readonly SessionStorageReader _sessionStorageReader;
        private readonly LockSessionFactory _lockSessionFactory;
        private readonly string _bucketName;

        public ObjectsManagementService(
            CephOptions cephOptions,
            IAmazonS3 amazonS3,
            TemplatesStorageReader templatesStorageReader,
            ObjectsStorageReader objectsStorageReader,
            SessionStorageReader sessionStorageReader,
            LockSessionFactory lockSessionFactory)
        {
            _amazonS3 = amazonS3;
            _templatesStorageReader = templatesStorageReader;
            _objectsStorageReader = objectsStorageReader;
            _sessionStorageReader = sessionStorageReader;
            _lockSessionFactory = lockSessionFactory;
            _bucketName = cephOptions.ObjectsBucketName;
        }

        private delegate IEnumerable<ObjectElementValidationError> ValidationRule(IObjectElementValue value, IElementConstraints constraints);

        public async Task<string> Create(long id, AuthorInfo authorInfo, IObjectDescriptor objectDescriptor)
        {
            CheckRequredProperties(id, objectDescriptor);

            LockSession lockSession = null;
            try
            {
                lockSession = await _lockSessionFactory.CreateLockSessionAsync(id);

                if (await _objectsStorageReader.IsObjectExists(id))
                {
                    throw new ObjectAlreadyExistsException(id);
                }

                var templateDescriptor = await _templatesStorageReader.GetTemplateDescriptor(objectDescriptor.TemplateId, objectDescriptor.TemplateVersionId);

                var latestTemplateVersionId = await _templatesStorageReader.GetTemplateLatestVersion(objectDescriptor.TemplateId);
                if (!templateDescriptor.VersionId.Equals(latestTemplateVersionId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Template '{objectDescriptor.TemplateId}' has an outdated version. " +
                        $"Latest versionId for template '{objectDescriptor.TemplateId}' is '{latestTemplateVersionId}'.");
                }

                if (templateDescriptor.Elements.Count != objectDescriptor.Elements.Count)
                {
                    throw new ObjectInconsistentException(
                        id,
                        $"Quantity of elements in the object doesn't match to the quantity of elements in the corresponding template with Id '{objectDescriptor.TemplateId}' and versionId '{objectDescriptor.TemplateVersionId}'.");
                }

                var elementIds = new HashSet<long>(objectDescriptor.Elements.Select(x => x.Id));
                if (elementIds.Count != objectDescriptor.Elements.Count)
                {
                    throw new ObjectInconsistentException(id, "Some elements have non-unique identifiers.");
                }

                EnsureObjectElementsState(id, templateDescriptor.Elements, objectDescriptor.Elements);

                return await PutObject(id, authorInfo, objectDescriptor);
            }
            finally
            {
                if (lockSession != null)
                {
                    await lockSession.ReleaseAsync();
                }
            }
        }

        public async Task<string> Modify(long id, string versionId, AuthorInfo authorInfo, IObjectDescriptor modifiedObjectDescriptor)
        {
            CheckRequredProperties(id, modifiedObjectDescriptor);

            if (string.IsNullOrEmpty(versionId))
            {
                throw new ArgumentException("Object version must be set", nameof(versionId));
            }

            LockSession lockSession = null;
            try
            {
                lockSession = await _lockSessionFactory.CreateLockSessionAsync(id);

                var objectDescriptor = await _objectsStorageReader.GetObjectDescriptor(id, null);
                if (objectDescriptor == null)
                {
                    throw new ObjectNotFoundException($"Object '{id}' not found.");
                }

                if (!versionId.Equals(objectDescriptor.VersionId, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ConcurrencyException(id, versionId, objectDescriptor.VersionId);
                }

                var currentTemplateIds = new HashSet<long>(objectDescriptor.Elements.Select(x => x.Id));
                var modifiedTemplateIds = new HashSet<long>(modifiedObjectDescriptor.Elements.Select(x => x.Id));
                if (!modifiedTemplateIds.IsSubsetOf(currentTemplateIds))
                {
                    throw new ObjectInconsistentException(id, "Modified object contains non-existing elements.");
                }

                EnsureObjectElementsState(id, objectDescriptor.Elements, modifiedObjectDescriptor.Elements);

                return await PutObject(id, authorInfo, modifiedObjectDescriptor);
            }
            finally
            {
                if (lockSession != null)
                {
                    await lockSession.ReleaseAsync();
                }
            }
        }

        private static void EnsureObjectElementsState(
            long objectId,
            IReadOnlyCollection<IElementDescriptor> referenceElementDescriptors,
            IEnumerable<IElementDescriptor> elementDescriptors)
        {
            var templateCodes = new HashSet<int>();
            foreach (var elementDescriptor in elementDescriptors)
            {
                var referenceObjectElement = referenceElementDescriptors.SingleOrDefault(x => x.TemplateCode == elementDescriptor.TemplateCode);
                if (referenceObjectElement == null)
                {
                    throw new ObjectInconsistentException(objectId, $"Element with template code '{elementDescriptor.TemplateCode}' not found in the template.");
                }

                if (!templateCodes.Add(elementDescriptor.TemplateCode))
                {
                    throw new ObjectInconsistentException(objectId, $"Element with template code '{elementDescriptor.TemplateCode}' must be unique within the object.");
                }

                if (referenceObjectElement.Type != elementDescriptor.Type)
                {
                    throw new ObjectInconsistentException(
                        objectId,
                        $"Type of the element with template code '{referenceObjectElement.TemplateCode}' ({referenceObjectElement.Type}) doesn't match to the type of corresponding element in template ({elementDescriptor.Type}).");
                }

                if (!referenceObjectElement.Constraints.Equals(elementDescriptor.Constraints))
                {
                    throw new ObjectInconsistentException(
                        objectId,
                        $"Constraints for the element with template code '{referenceObjectElement.TemplateCode}' doesn't match to constraints for corresponding element in template.");
                }
            }
        }

        private static void CheckRequredProperties(long id, IObjectPersistenceDescriptor objectDescriptor)
        {
            if (id <= 0)
            {
                throw new ArgumentException("Object Id must be set", nameof(id));
            }

            if (objectDescriptor.Language == Language.Unspecified)
            {
                throw new ArgumentException("Language must be specified.", nameof(objectDescriptor.Language));
            }

            if (objectDescriptor.TemplateId <= 0)
            {
                throw new ArgumentException("Template Id must be specified", nameof(objectDescriptor.TemplateId));
            }

            if (string.IsNullOrEmpty(objectDescriptor.TemplateVersionId))
            {
                throw new ArgumentException("Template versionId must be specified", nameof(objectDescriptor.TemplateVersionId));
            }

            if (objectDescriptor.Properties == null)
            {
                throw new ArgumentException("Object properties must be specified", nameof(objectDescriptor.Properties));
            }
        }

        private static async Task VerifyObjectElementsConsistency(
            long objectId,
            Language language,
            IEnumerable<IObjectElementDescriptor> elementDescriptors)
        {
            var tasks = elementDescriptors.Select(
                async x => await Task.Run(
                               () =>
                                   {
                                       var errors = new List<ObjectElementValidationError>();
                                       var constraints = x.Constraints.For(language);
                                       var rules = GetValidationRules(x);

                                       foreach (var validationRule in rules)
                                       {
                                           errors.AddRange(validationRule(x.Value, constraints));
                                       }

                                       if (errors.Count > 0)
                                       {
                                           throw new InvalidObjectElementException(objectId, x.Id, errors);
                                       }
                                   }));
            await Task.WhenAll(tasks);
        }

        private static IEnumerable<ValidationRule> GetValidationRules(IObjectElementDescriptor descriptor)
        {
            switch (descriptor.Type)
            {
                case ElementDescriptorType.PlainText:
                    return new ValidationRule[]
                        {
                            PlainTextValidator.CheckLength,
                            PlainTextValidator.CheckWordsLength,
                            PlainTextValidator.CheckLinesCount,
                            PlainTextValidator.CheckRestrictedSymbols
                        };
                case ElementDescriptorType.FormattedText:
                    return new ValidationRule[]
                        {
                            FormattedTextValidator.CheckLength,
                            FormattedTextValidator.CheckWordsLength,
                            FormattedTextValidator.CheckLinesCount,
                            FormattedTextValidator.CheckRestrictedSymbols,
                            FormattedTextValidator.CheckValidHtml,
                            FormattedTextValidator.CheckSupportedHtmlTags,
                            FormattedTextValidator.CheckAttributesAbsence,
                            FormattedTextValidator.CheckEmptyList,
                            FormattedTextValidator.CheckNestedList,
                            FormattedTextValidator.CheckUnsupportedListElements
                        };
                case ElementDescriptorType.FasComment:
                    return new ValidationRule[]
                               {
                                   PlainTextValidator.CheckLength,
                                   PlainTextValidator.CheckWordsLength,
                                   PlainTextValidator.CheckLinesCount,
                                   PlainTextValidator.CheckRestrictedSymbols
                               };
                case ElementDescriptorType.Image:
                case ElementDescriptorType.Article:
                    return new ValidationRule[] { BinaryValidator.CheckFilename };
                case ElementDescriptorType.Date:
                    return new ValidationRule[] { DateValidator.CheckDate };
                case ElementDescriptorType.Link:
                    return new ValidationRule[]
                               {
                                   LinkValidator.CheckLink,
                                   PlainTextValidator.CheckLength,
                                   PlainTextValidator.CheckWordsLength,
                                   PlainTextValidator.CheckLinesCount,
                                   PlainTextValidator.CheckRestrictedSymbols
                               };
                case ElementDescriptorType.Phone:
                    return new ValidationRule[] { };
                case ElementDescriptorType.VideoLink:
                    return new ValidationRule[] { LinkValidator.CheckLink };
                default:
                    throw new ArgumentOutOfRangeException(nameof(descriptor.Type), descriptor.Type, $"Unsupported element descriptor type for descriptor {descriptor.Id}");
            }
        }

        private async Task<string> PutObject(long id, AuthorInfo authorInfo, IObjectDescriptor objectDescriptor)
        {
            await PreprocessObjectElements(objectDescriptor.Elements);
            await VerifyObjectElementsConsistency(id, objectDescriptor.Language, objectDescriptor.Elements);
            var metadataForBinaries = await RetrieveMetadataForBinaries(id, objectDescriptor.Elements);

            PutObjectRequest putRequest;
            MetadataCollectionWrapper metadataWrapper;

            foreach (var elementDescriptor in objectDescriptor.Elements)
            {
                var value = elementDescriptor.Value;
                if (elementDescriptor.Value is IBinaryElementValue binaryElementValue)
                {
                    if (string.IsNullOrEmpty(binaryElementValue.Raw))
                    {
                        value = new BinaryElementPersistenceValue(null, null, null);
                    }
                    else
                    {
                        var metadata = metadataForBinaries[elementDescriptor.Id];
                        value = new BinaryElementPersistenceValue(binaryElementValue.Raw, metadata.Filename, metadata.Filesize);
                    }
                }

                var elementPersistenceDescriptor = new ObjectElementPersistenceDescriptor(elementDescriptor, value);
                putRequest = new PutObjectRequest
                                 {
                                     Key = id.AsS3ObjectKey(elementDescriptor.Id),
                                     BucketName = _bucketName,
                                     ContentType = ContentType.Json,
                                     ContentBody = JsonConvert.SerializeObject(elementPersistenceDescriptor, SerializerSettings.Default),
                                     CannedACL = S3CannedACL.PublicRead
                                 };

                metadataWrapper = MetadataCollectionWrapper.For(putRequest.Metadata);
                metadataWrapper.Write(MetadataElement.Author, authorInfo.Author);
                metadataWrapper.Write(MetadataElement.AuthorLogin, authorInfo.AuthorLogin);
                metadataWrapper.Write(MetadataElement.AuthorName, authorInfo.AuthorName);

                await _amazonS3.PutObjectAsync(putRequest);
            }

            var objectKey = id.AsS3ObjectKey(Tokens.ObjectPostfix);
            var objectVersions = await _objectsStorageReader.GetObjectLatestVersions(id);
            var elementVersions = objectVersions.Where(x => !x.Id.EndsWith(Tokens.ObjectPostfix)).ToArray();
            var objectPersistenceDescriptor = new ObjectPersistenceDescriptor
                                                  {
                                                      TemplateId = objectDescriptor.TemplateId,
                                                      TemplateVersionId = objectDescriptor.TemplateVersionId,
                                                      Language = objectDescriptor.Language,
                                                      Properties = objectDescriptor.Properties,
                                                      Elements = elementVersions
                                                  };
            putRequest = new PutObjectRequest
                             {
                                 Key = objectKey,
                                 BucketName = _bucketName,
                                 ContentType = ContentType.Json,
                                 ContentBody = JsonConvert.SerializeObject(objectPersistenceDescriptor, SerializerSettings.Default),
                                 CannedACL = S3CannedACL.PublicRead
                             };

            metadataWrapper = MetadataCollectionWrapper.For(putRequest.Metadata);
            metadataWrapper.Write(MetadataElement.Author, authorInfo.Author);
            metadataWrapper.Write(MetadataElement.AuthorLogin, authorInfo.AuthorLogin);
            metadataWrapper.Write(MetadataElement.AuthorName, authorInfo.AuthorName);

            metadataWrapper.Write(
                MetadataElement.ModifiedElements,
                string.Join(Tokens.ModifiedElementsDelimiter.ToString(), objectDescriptor.Elements.Select(x => x.TemplateCode)));

            await _amazonS3.PutObjectAsync(putRequest);

            objectVersions = await _objectsStorageReader.GetObjectLatestVersions(id);
            return objectVersions.Where(x => x.Id.EndsWith(Tokens.ObjectPostfix))
                                 .Select(x => x.VersionId)
                                 .Single();
        }

        private async Task PreprocessObjectElements(IEnumerable<IObjectElementDescriptor> elementDescriptors)
        {
            var tasks = elementDescriptors.Select(
                async descriptor =>
                    await Task.Run(
                        () =>
                            {
                                switch (descriptor.Type)
                                {
                                    case ElementDescriptorType.PlainText:
                                        ((TextElementValue)descriptor.Value).Raw =
                                            ElementTextHarmonizer.ProcessPlain(((TextElementValue)descriptor.Value).Raw);
                                        break;
                                    case ElementDescriptorType.FormattedText:
                                        ((TextElementValue)descriptor.Value).Raw =
                                            ElementTextHarmonizer.ProcessFormatted(((TextElementValue)descriptor.Value).Raw);
                                        break;
                                    case ElementDescriptorType.FasComment:
                                        ((FasElementValue)descriptor.Value).Text =
                                            ElementTextHarmonizer.ProcessPlain(((FasElementValue)descriptor.Value).Text);
                                        break;
                                    case ElementDescriptorType.Image:
                                    case ElementDescriptorType.Article:
                                    case ElementDescriptorType.Date:
                                    case ElementDescriptorType.Link:
                                    case ElementDescriptorType.Phone:
                                    case ElementDescriptorType.VideoLink:
                                        break;
                                    default:
                                        throw new ArgumentOutOfRangeException(nameof(descriptor.Type),
                                                                              descriptor.Type,
                                                                              $"Unsupported element descriptor type for descriptor {descriptor.Id}");
                                }
                            }));

            await Task.WhenAll(tasks);
        }

        private async Task<IReadOnlyDictionary<long, BinaryMetadata>> RetrieveMetadataForBinaries(
            long id,
            IEnumerable<IObjectElementDescriptor> objectElements)
        {
            var tasks = objectElements
                .Select(async x =>
                            {
                                var binaryElementValue = x.Value as IBinaryElementValue;
                                if (binaryElementValue == null || string.IsNullOrEmpty(binaryElementValue.Raw))
                                {
                                    return (Id: null, Metadata: null);
                                }

                                try
                                {
                                    var binaryMetadata = await _sessionStorageReader.GetBinaryMetadata(binaryElementValue.Raw);
                                    return (Id: (long?)x.Id, Metadata: binaryMetadata);
                                }
                                catch (ObjectNotFoundException ex)
                                {
                                    throw new InvalidObjectElementException(id, x.Id, new[] { new BinaryNotFoundError(binaryElementValue.Raw) }, ex);
                                }
                            })
                .ToList();
            await Task.WhenAll(tasks);

            return tasks.Select(x => x.Result).Where(x => x.Id.HasValue).ToDictionary(x => x.Id.Value, x => x.Metadata);
        }
    }
}
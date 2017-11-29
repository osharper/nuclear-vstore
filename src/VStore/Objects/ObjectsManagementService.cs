using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;

using NuClear.VStore.DataContract;
using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Objects.Persistence;
using NuClear.VStore.Descriptors.Sessions;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Events;
using NuClear.VStore.Http;
using NuClear.VStore.Json;
using NuClear.VStore.Kafka;
using NuClear.VStore.Locks;
using NuClear.VStore.Objects.ContentPreprocessing;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.Errors;
using NuClear.VStore.Options;
using NuClear.VStore.Prometheus;
using NuClear.VStore.S3;
using NuClear.VStore.Sessions;
using NuClear.VStore.Sessions.ContentValidation;
using NuClear.VStore.Templates;

using Prometheus.Client;

namespace NuClear.VStore.Objects
{
    public sealed class ObjectsManagementService
    {
        private readonly IS3Client _s3Client;
        private readonly TemplatesStorageReader _templatesStorageReader;
        private readonly ObjectsStorageReader _objectsStorageReader;
        private readonly SessionStorageReader _sessionStorageReader;
        private readonly DistributedLockManager _distributedLockManager;
        private readonly EventSender _eventSender;
        private readonly string _bucketName;
        private readonly string _objectEventsTopic;
        private readonly Counter _referencedBinariesMetric;

        public ObjectsManagementService(
            CephOptions cephOptions,
            KafkaOptions kafkaOptions,
            IS3Client s3Client,
            TemplatesStorageReader templatesStorageReader,
            ObjectsStorageReader objectsStorageReader,
            SessionStorageReader sessionStorageReader,
            DistributedLockManager distributedLockManager,
            EventSender eventSender,
            MetricsProvider metricsProvider)
        {
            _s3Client = s3Client;
            _templatesStorageReader = templatesStorageReader;
            _objectsStorageReader = objectsStorageReader;
            _sessionStorageReader = sessionStorageReader;
            _distributedLockManager = distributedLockManager;
            _eventSender = eventSender;
            _bucketName = cephOptions.ObjectsBucketName;
            _objectEventsTopic = kafkaOptions.ObjectEventsTopic;
            _referencedBinariesMetric = metricsProvider.GetReferencedBinariesMetric();
        }

        private delegate IEnumerable<ObjectElementValidationError> ValidationRule(IObjectElementValue value, IElementConstraints constraints);

        public async Task<string> Create(long id, AuthorInfo authorInfo, IObjectDescriptor objectDescriptor)
        {
            CheckRequredProperties(id, objectDescriptor);

            var redLock = await _distributedLockManager.CreateLockAsync(id);
            try
            {
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

                return await PutObject(id, null, authorInfo, objectDescriptor);
            }
            finally
            {
                redLock?.Dispose();
            }
        }

        public async Task<string> Modify(long id, string versionId, AuthorInfo authorInfo, IObjectDescriptor modifiedObjectDescriptor)
        {
            CheckRequredProperties(id, modifiedObjectDescriptor);

            if (string.IsNullOrEmpty(versionId))
            {
                throw new ArgumentException("Object version must be set", nameof(versionId));
            }

            var redLock = await _distributedLockManager.CreateLockAsync(id);
            try
            {
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

                return await PutObject(id, versionId, authorInfo, modifiedObjectDescriptor);
            }
            finally
            {
                redLock?.Dispose();
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
            var allErrors = new ConcurrentDictionary<int, IReadOnlyCollection<ObjectElementValidationError>>();
            var tasks = elementDescriptors.Select(
                async element =>
                    await Task.Run(() =>
                                       {
                                           var errors = new List<ObjectElementValidationError>();
                                           var constraints = element.Constraints.For(language);
                                           var rules = GetValidationRules(element);

                                           foreach (var validationRule in rules)
                                           {
                                               errors.AddRange(validationRule(element.Value, constraints));
                                           }

                                           if (errors.Count > 0)
                                           {
                                               allErrors[element.TemplateCode] = errors;
                                           }
                                       }));

            await Task.WhenAll(tasks);

            if (allErrors.Count > 0)
            {
                throw new InvalidObjectException(objectId, allErrors);
            }
        }

        private static IEnumerable<ValidationRule> GetValidationRules(IObjectElementDescriptor descriptor)
        {
            switch (descriptor.Type)
            {
                case ElementDescriptorType.PlainText:
                case ElementDescriptorType.FasComment:
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
                case ElementDescriptorType.Link:
                case ElementDescriptorType.VideoLink:
                    return new ValidationRule[]
                        {
                            LinkValidator.CheckLink,
                            PlainTextValidator.CheckLength,
                            PlainTextValidator.CheckRestrictedSymbols
                        };
                case ElementDescriptorType.BitmapImage:
                case ElementDescriptorType.VectorImage:
                case ElementDescriptorType.Article:
                case ElementDescriptorType.Phone:
                    return new ValidationRule[] { };
                case ElementDescriptorType.Color:
                    return new ValidationRule[] { ColorValidator.CheckValidColor };
                case ElementDescriptorType.Logo:
                    return new ValidationRule[] { LogoValidator.CheckValidLogo };
                default:
                    throw new ArgumentOutOfRangeException(nameof(descriptor.Type), descriptor.Type, $"Unsupported element descriptor type for descriptor {descriptor.Id}");
            }
        }

        private async Task<string> PutObject(long id, string versionId, AuthorInfo authorInfo, IObjectDescriptor objectDescriptor)
        {
            PreprocessObjectElements(objectDescriptor.Elements);
            await VerifyObjectElementsConsistency(id, objectDescriptor.Language, objectDescriptor.Elements);
            var metadataForBinaries = await RetrieveMetadataForBinaries(id, objectDescriptor.Elements);

            await _eventSender.SendAsync(_objectEventsTopic, new ObjectVersionCreatingEvent(id, versionId));

            var binariesCount = 0;
            PutObjectRequest putRequest;
            MetadataCollectionWrapper metadataWrapper;

            foreach (var elementDescriptor in objectDescriptor.Elements)
            {
                var (elementPersistenceValue, referencedBinariesCount) = Convert2PersistenceValue(elementDescriptor.Value, metadataForBinaries);
                var elementPersistenceDescriptor = new ObjectElementPersistenceDescriptor(elementDescriptor, elementPersistenceValue);
                binariesCount += referencedBinariesCount;
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

                await _s3Client.PutObjectAsync(putRequest);
            }

            var objectKey = id.AsS3ObjectKey(Tokens.ObjectPostfix);
            var objectVersions = await _objectsStorageReader.GetObjectLatestVersions(id);
            var elementVersions = objectVersions.Where(x => !x.Id.EndsWith(Tokens.ObjectPostfix)).ToList();
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

            await _s3Client.PutObjectAsync(putRequest);
            _referencedBinariesMetric.Inc(binariesCount);

            objectVersions = await _objectsStorageReader.GetObjectLatestVersions(id);
            return objectVersions.Where(x => x.Id.EndsWith(Tokens.ObjectPostfix))
                                 .Select(x => x.VersionId)
                                 .Single();
        }

        private static (IObjectElementValue, int) Convert2PersistenceValue(IObjectElementValue elementValue,
                                                                           IReadOnlyDictionary<string, BinaryMetadata> metadataForBinaries)
        {
            if (!(elementValue is IBinaryElementValue binaryElementValue))
            {
                return (elementValue, 0);
            }

            if (string.IsNullOrEmpty(binaryElementValue.Raw))
            {
                return (BinaryElementPersistenceValue.Empty, 0);
            }

            if (binaryElementValue is ILogoElementValue logoElementValue)
            {
                var originalMeta = metadataForBinaries[binaryElementValue.Raw];
                var customImages = logoElementValue.CustomImages
                                                   .Select(x =>
                                                               {
                                                                   var customImageMeta = metadataForBinaries[x.Raw];
                                                                   return new CustomImage
                                                                       {
                                                                           Filename = customImageMeta.Filename,
                                                                           Filesize = customImageMeta.Filesize,
                                                                           Raw = x.Raw,
                                                                           Size = x.Size
                                                                       };
                                                               })
                                                   .ToList();
                var persistenceValue = new LogoElementPersistenceValue(logoElementValue.Raw,
                                                                       originalMeta.Filename,
                                                                       originalMeta.Filesize,
                                                                       logoElementValue.CropArea,
                                                                       customImages);
                return (persistenceValue, customImages.Count + 1);
            }

            var metadata = metadataForBinaries[binaryElementValue.Raw];
            return (new BinaryElementPersistenceValue(binaryElementValue.Raw, metadata.Filename, metadata.Filesize), 1);
        }

        private void PreprocessObjectElements(IEnumerable<IObjectElementDescriptor> elementDescriptors)
        {
            foreach (var descriptor in elementDescriptors)
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
                    case ElementDescriptorType.VideoLink:
                    case ElementDescriptorType.Link:
                        ((TextElementValue)descriptor.Value).Raw =
                            ElementTextHarmonizer.ProcessLink(((TextElementValue)descriptor.Value).Raw);
                        break;
                    case ElementDescriptorType.BitmapImage:
                    case ElementDescriptorType.VectorImage:
                    case ElementDescriptorType.Article:
                    case ElementDescriptorType.Phone:
                    case ElementDescriptorType.Color:
                    case ElementDescriptorType.Logo:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(descriptor.Type),
                                                              descriptor.Type,
                                                              $"Unsupported element descriptor type for descriptor {descriptor.Id}");
                }
            }
        }

        private async Task<IReadOnlyDictionary<string, BinaryMetadata>> RetrieveMetadataForBinaries(
            long id,
            IEnumerable<IObjectElementDescriptor> objectElements)
        {
            var elementsRawValues = objectElements.SelectMany(x => x.Value.ExtractFileKeys().Select(y => new { x.TemplateCode, Raw = y }));
            var tasks = elementsRawValues.Select(async x =>
                                                     {
                                                         try
                                                         {
                                                             await _sessionStorageReader.VerifySessionExpirationForBinary(x.Raw);

                                                             var binaryMetadata = await _sessionStorageReader.GetBinaryMetadata(x.Raw);
                                                             return (TemplateCode: x.TemplateCode, Raw: x.Raw, Metadata: binaryMetadata, Error: (ObjectElementValidationError)null);
                                                         }
                                                         catch (ObjectNotFoundException)
                                                         {
                                                             return (TemplateCode: x.TemplateCode, Raw: x.Raw, Metadata: null, Error: new BinaryNotFoundError(x.Raw));
                                                         }
                                                         catch (SessionExpiredException)
                                                         {
                                                             return (TemplateCode: x.TemplateCode, Raw: x.Raw, Metadata: null, Error: new BinaryNotFoundError(x.Raw));
                                                         }
                                                     })
                                         .ToList();

            var results = await Task.WhenAll(tasks);

            var errors = results.Where(x => x.Error != null)
                                .GroupBy(x => x.TemplateCode)
                                .ToDictionary(x => x.Key, x => (IReadOnlyCollection<ObjectElementValidationError>)x.Select(e => e.Error).ToList());
            if (errors.Count > 0)
            {
                throw new InvalidObjectException(id, errors);
            }

            return results.Where(x => x.Metadata != null)
                          .ToDictionary(x => x.Raw, x => x.Metadata);
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;
using NuClear.VStore.Locks;
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

        public async Task<string> Create(long id, IObjectDescriptor objectDescriptor)
        {
            if (objectDescriptor.Language == Language.Unspecified)
            {
                throw new InvalidOperationException("Language must be explicitly specified.");
            }

            if (await _objectsStorageReader.IsObjectExists(id))
            {
                throw new ObjectAlreadyExistsException(id);
            }

            await EnsureObjectTemplateState(id, objectDescriptor);
            EnsureAllBinariesExist(id, objectDescriptor.Elements);

            return await PutObject(id, objectDescriptor);
        }

        public async Task<string> ModifyElement(long objectId, string versionId, IObjectDescriptor objectDescriptor)
        {
            if (objectId == 0)
            {
                throw new ArgumentException("Object Id must be set", nameof(objectId));
            }

            if (string.IsNullOrEmpty(versionId))
            {
                throw new ArgumentException("VersionId must be set", nameof(versionId));
            }

            using (_lockSessionFactory.CreateLockSession(objectId))
            {
                var descriptorKey = objectId.AsS3ObjectKey(Tokens.ObjectPostfix);
                await EnsureObjectTemplateState(objectId, objectDescriptor);
                await EnsureObjectState(descriptorKey, versionId);
                EnsureAllBinariesExist(objectId, objectDescriptor.Elements);

                return await PutObject(objectId, objectDescriptor);
            }
        }

        private IEnumerable<ValidationRule> GetVerificationRules(IObjectElementDescriptor descriptor, IElementConstraints elementConstraints)
        {
            switch (descriptor.Type)
            {
                case ElementDescriptorType.Text:
                    return ((TextElementConstraints)elementConstraints).IsFormatted
                               ? new ValidationRule[]
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
                                     }
                               : new ValidationRule[]
                                     {
                                         PlainTextValidator.CheckLength,
                                         PlainTextValidator.CheckWordsLength,
                                         PlainTextValidator.CheckLinesCount,
                                         PlainTextValidator.CheckRestrictedSymbols
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
                default:
                    throw new ArgumentOutOfRangeException(nameof(descriptor.Type), descriptor.Type, $"Unsupported element descriptor type for descriptor {descriptor.Id}");
            }
        }

        private void VerifyObjectElementsConsistency(long objectId, Language language, IEnumerable<IObjectElementDescriptor> elementDescriptors)
        {
            Parallel.ForEach(
                elementDescriptors,
                elementDescriptor =>
                {
                    var errors = new List<ObjectElementValidationError>();
                    var constraints = elementDescriptor.Constraints.For(language);
                    var rules = GetVerificationRules(elementDescriptor, constraints);

                    foreach (var validationRule in rules)
                    {
                        errors.AddRange(validationRule(elementDescriptor.Value, constraints));
                    }

                    if (errors.Count > 0)
                    {
                        throw new InvalidObjectElementException(objectId, elementDescriptor.Id, errors);
                    }
                });
        }

        private async Task<string> PutObject(long id, IObjectDescriptor objectDescriptor)
        {
            VerifyObjectElementsConsistency(id, objectDescriptor.Language, objectDescriptor.Elements);

            PutObjectRequest putRequest;
            foreach (var elementDescriptor in objectDescriptor.Elements)
            {
                putRequest = new PutObjectRequest
                                 {
                                     Key = id.AsS3ObjectKey(elementDescriptor.Id),
                                     BucketName = _bucketName,
                                     ContentType = ContentType.Json,
                                     ContentBody = JsonConvert.SerializeObject(elementDescriptor, SerializerSettings.Default),
                                     CannedACL = S3CannedACL.PublicRead
                                 };
                await _amazonS3.PutObjectAsync(putRequest);
            }

            var objectKey = id.AsS3ObjectKey(Tokens.ObjectPostfix);
            var objectVersions = await _objectsStorageReader.GetObjectLatestVersions(id);
            var objectPersistenceDescriptor = new ObjectPersistenceDescriptor
                {
                    TemplateId = objectDescriptor.TemplateId,
                    TemplateVersionId = objectDescriptor.TemplateVersionId,
                    Language = objectDescriptor.Language,
                    Properties = objectDescriptor.Properties,
                    Elements = objectVersions
                        .Where(x => !x.Key.Equals(objectKey, StringComparison.OrdinalIgnoreCase))
                        .ToArray()
                };

            putRequest = new PutObjectRequest
                             {
                                 Key = objectKey,
                                 BucketName = _bucketName,
                                 ContentType = ContentType.Json,
                                 ContentBody = JsonConvert.SerializeObject(objectPersistenceDescriptor, SerializerSettings.Default),
                                 CannedACL = S3CannedACL.PublicRead
                             };
            await _amazonS3.PutObjectAsync(putRequest);

            objectVersions = await _objectsStorageReader.GetObjectLatestVersions(id);
            return objectVersions.Where(x => x.Key.Equals(objectKey, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.VersionId)
                .Single();
        }

        private async Task EnsureObjectTemplateState(long id, IObjectDescriptor objectDescriptor)
        {
            var templateDescriptor = await _templatesStorageReader.GetTemplateDescriptor(objectDescriptor.TemplateId, objectDescriptor.TemplateVersionId);

            var latestTemplateVersionId = await _templatesStorageReader.GetTemplateLatestVersion(objectDescriptor.TemplateId);
            if (!templateDescriptor.VersionId.Equals(latestTemplateVersionId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Template '{objectDescriptor.TemplateId}' has an outdated version. " +
                                                    $"Latest versionId for template '{objectDescriptor.TemplateId}' is '{latestTemplateVersionId}'.");
            }

            if (templateDescriptor.Elements.Count != objectDescriptor.Elements.Count)
            {
                throw new ObjectInconsistentException(id, "Number of elements in the object doesn't match to the number of elements in corresponding template " +
                                                          $"with Id '{templateDescriptor.Id}' and Version Id '{templateDescriptor.VersionId}'.");
            }

            foreach (var templateElement in templateDescriptor.Elements)
            {
                var objectElements = objectDescriptor.Elements.Where(x => x.TemplateCode == templateElement.TemplateCode).ToArray();
                if (objectElements.Length == 0)
                {
                    throw new ObjectInconsistentException(id, $"Element with template code '{templateElement.TemplateCode}' not found in the object.");
                }

                if (objectElements.Length > 1)
                {
                    throw new ObjectInconsistentException(id, $"Element with template code '{templateElement.TemplateCode}' must be unique within the object.");
                }

                var objectElement = objectElements[0];
                if (objectElement.Type != templateElement.Type)
                {
                    throw new ObjectInconsistentException(id, $"Type of the element with template code '{objectElement.TemplateCode}' ({objectElement.Type}) " +
                                                              $"doesn't match to the type of corresponding element in template ({templateElement.Type}).");
                }

                if (!objectElement.Constraints.Equals(templateElement.Constraints))
                {
                    throw new ObjectInconsistentException(id, $"Constraints for the element with template code '{objectElement.TemplateCode}' " +
                                                              "doesn't match to constraints for corresponding element in template.");
                }
            }
        }

        private void EnsureAllBinariesExist(long id, IEnumerable<IObjectElementDescriptor> objectElements)
        {
            Parallel.ForEach(objectElements,
                             objectElement =>
                                 {
                                     var binaryValue = objectElement.Value as IBinaryElementValue;
                                     if (binaryValue != null && !_sessionStorageReader.IsBinaryExists(binaryValue.Raw).Result)
                                     {
                                         throw new InvalidObjectElementException(id, objectElement.Id, new[] { new BinaryNotFoundError(binaryValue.Raw) });
                                     }
                                 });
        }

        private async Task EnsureObjectState(string key, string versionId)
        {
            var versionsResponse = await _amazonS3.ListVersionsAsync(_bucketName, key);
            if (versionsResponse.Versions.Count == 0)
            {
                throw new ObjectNotFoundException($"Object '{key}' not found.");
            }

            var latestVersionId = versionsResponse.Versions.Find(x => x.IsLatest).VersionId;
            if (!versionId.Equals(latestVersionId, StringComparison.Ordinal))
            {
                throw new ConcurrencyException(key, versionId, latestVersionId);
            }
        }
    }
}
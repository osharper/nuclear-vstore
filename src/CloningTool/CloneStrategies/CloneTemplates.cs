using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CloningTool.Json;
using CloningTool.RestClient;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;

namespace CloningTool.CloneStrategies
{
    public class CloneTemplates : ICloneStrategy
    {
        private readonly CloningToolOptions _options;
        private readonly ILogger<CloneTemplates> _logger;
        private readonly JTokenEqualityComparer _jsonEqualityComparer = new JTokenEqualityComparer();

        private IDictionary<long, ApiListTemplate> _destTemplates;
        private IDictionary<long, ApiListTemplate> _sourceTemplates;

        public CloneTemplates(CloningToolOptions options,
            IReadOnlyRestClientFacade sourceRestClient,
            IRestClientFacade destRestClient,
            ILogger<CloneTemplates> logger)
        {
            _options = options;
            _logger = logger;
            SourceRestClient = sourceRestClient;
            DestRestClient = destRestClient;
        }

        public IReadOnlyRestClientFacade SourceRestClient { get; }

        public IRestClientFacade DestRestClient { get; }

        public async Task<bool> ExecuteAsync()
        {
            await EnsureTemplatesAreLoaded();
            if (_destTemplates.Count > 0)
            {
                var diff = new HashSet<long>(_sourceTemplates.Keys);
                diff.SymmetricExceptWith(_destTemplates.Keys);
                if (diff.Count > 0)
                {
                    var missedInSource = diff.Where(d => !_sourceTemplates.ContainsKey(d)).ToList();
                    if (missedInSource.Count > 0)
                    {
                        _logger.LogWarning("Next {count} templates are not present in source: {list}", missedInSource.Count, missedInSource);
                    }

                    var missedInDest = diff.Where(d => !_destTemplates.ContainsKey(d)).ToList();
                    if (missedInDest.Count > 0)
                    {
                        _logger.LogInformation("Next {count} templates are not present in destination: {list}", missedInDest.Count, missedInDest);
                    }
                }
                else
                {
                    _logger.LogInformation("All {count} templates are present both in source and destination", _sourceTemplates.Count);
                }
            }
            else
            {
                _logger.LogInformation("There are no templates in destination and total {count} templates in source", _sourceTemplates.Count);
            }

            var clonedCount = 0L;
            var failedIds = new ConcurrentBag<long>();
            await CloneHelpers.ParallelRunAsync(_sourceTemplates.Values, _options.MaxDegreeOfParallelism,
                                                async template =>
                                                {
                                                    try
                                                    {
                                                        await CloneTemplateAsync(template);
                                                        _logger.LogInformation("Template cloning succeeded: {template}", template);
                                                        Interlocked.Increment(ref clonedCount);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        failedIds.Add(template.Id);
                                                        _logger.LogError(new EventId(), ex, "Template cloning error: {template}", template);
                                                    }
                                                });

            _logger.LogInformation("Cloned templates: {cloned} of {total}", clonedCount, _sourceTemplates.Count);
            if (failedIds.Count > 0)
            {
                _logger.LogWarning("Id's of failed templates: {list}", failedIds);
                return false;
            }

            return true;
        }

        private async Task EnsureTemplatesAreLoaded()
        {
            _sourceTemplates = _sourceTemplates ?? (await SourceRestClient.GetTemplatesAsync()).ToDictionary(p => p.Id);
            _destTemplates = _destTemplates ?? (await DestRestClient.GetTemplatesAsync()).ToDictionary(p => p.Id);
        }

        private async Task CloneTemplateAsync(ApiListTemplate template)
        {
            var templateIdStr = template.Id.ToString();
            var sourceTemplate = await SourceRestClient.GetTemplateAsync(templateIdStr);
            var destTemplate = await DestRestClient.GetTemplateAsync(templateIdStr);
            if (destTemplate == null)
            {
                await DestRestClient.CreateTemplateAsync(templateIdStr, sourceTemplate);
            }
            else
            {
                _logger.LogInformation("Template {id} already exists in destination", template.Id);
                if (!CompareTemplateDescriptors(destTemplate, sourceTemplate))
                {
                    if (_options.OverwriteUnequalTemplates)
                    {
                        _logger.LogWarning("Updating template {id} with version {versionId} in destination", template.Id, destTemplate.VersionId);
                        await DestRestClient.UpdateTemplateAsync(sourceTemplate, destTemplate.VersionId);
                    }
                    else
                    {
                        throw new InvalidOperationException("Templates with id = " + templateIdStr + " are not equal");
                    }
                }
                else
                {
                    _logger.LogInformation("Templates with id {id} are equal in source and destination", template.Id);
                }
            }
        }

        private bool CompareTemplateDescriptors(TemplateDescriptor existedTemplate, TemplateDescriptor newTemplate)
        {
            var firstProps = existedTemplate.Properties
                .Properties()
                .Where(p => p.Name != Tokens.NameToken)
                .ToList();

            var secondProps = newTemplate.Properties
                .Properties()
                .Where(p => p.Name != Tokens.NameToken)
                .ToList();

            if (existedTemplate.Id != newTemplate.Id
                   || firstProps.Count != secondProps.Count
                   || existedTemplate.Elements.Count != newTemplate.Elements.Count
                   || !firstProps.SequenceEqual(secondProps, _jsonEqualityComparer))
            {
                var first = new { existedTemplate.Id, Elements = existedTemplate.Elements.Count, Props = new JObject(firstProps) };
                var second = new { newTemplate.Id, Elements = newTemplate.Elements.Count, Props = new JObject(secondProps) };
                _logger.LogInformation("Different template headers for template {id}, existed: {existed} and new: {new}", existedTemplate.Id, first, second);
                return false;
            }

            foreach (var pair in existedTemplate.Elements.Zip(newTemplate.Elements, (e, g) => new { e, g }))
            {
                var firstElement = pair.e;
                var secondElement = pair.g;
                var firstConstraints = (IReadOnlyDictionary<Language, IElementConstraints>)firstElement.Constraints;
                var secondConstraints = (IReadOnlyDictionary<Language, IElementConstraints>)secondElement.Constraints;

                var firstElementProps = firstElement.Properties
                    .Properties()
                    .Where(p => p.Name != Tokens.NameToken)
                    .ToList();

                var secondElementProps = secondElement.Properties
                    .Properties()
                    .Where(p => p.Name != Tokens.NameToken)
                    .ToList();

                if (firstElement.TemplateCode != secondElement.TemplateCode
                    || firstElement.Type != secondElement.Type
                    || firstConstraints.Count != secondConstraints.Count
                    || firstElementProps.Count != secondElementProps.Count
                    || !firstElementProps.SequenceEqual(secondElementProps, _jsonEqualityComparer))
                {
                    var first = new { firstElement.TemplateCode, Type = firstElement.Type.ToString(), Constraints = firstConstraints.Count, Props = new JObject(firstElementProps) };
                    var second = new { secondElement.TemplateCode, Type = secondElement.Type.ToString(), Constraints = secondConstraints.Count, Props = new JObject(secondElementProps) };
                    _logger.LogInformation("Different elements headers for template {id}, existed: {existed} and new: {new}", existedTemplate.Id, first, second);
                    return false;
                }

                var firstConstraint = firstElement.Constraints.For(Language.Unspecified);
                var secondConstraint = secondElement.Constraints.For(Language.Unspecified);

                if (!Equals(firstConstraint, secondConstraint))
                {
                    var first = JsonConvert.SerializeObject(firstConstraint, SerializerSettings.Default);
                    var second = JsonConvert.SerializeObject(secondConstraint, SerializerSettings.Default);
                    _logger.LogInformation("Different element constraints for template {id}, existed: {existed} and new: {new}", existedTemplate.Id, first, second);
                }
            }

            return true;
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using CloningTool.Json;
using CloningTool.RestClient;

using Microsoft.Extensions.Logging;

namespace CloningTool.CloneStrategies
{
    public class ClonePositionLinks : ICloneStrategy
    {
        private readonly CloningToolOptions _options;
        private readonly ILogger<ClonePositionLinks> _logger;

        public ClonePositionLinks(
            CloningToolOptions options,
            IReadOnlyRestClientFacade sourceRestClient,
            IRestClientFacade destRestClient,
            ILogger<ClonePositionLinks> logger)
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
            var sourcePositions = (await SourceRestClient.GetContentPositionsAsync()).ToDictionary(p => p.Id);
            var destPositions = (await DestRestClient.GetContentPositionsAsync()).ToDictionary(p => p.Id);
            if (destPositions.Count == 0 && sourcePositions.Count > 0)
            {
                throw new InvalidOperationException($"There are {sourcePositions.Count} positions in source, but no positions in destination");
            }

            var diff = new HashSet<long>(sourcePositions.Keys);
            diff.SymmetricExceptWith(destPositions.Keys);
            if (diff.Count > 0)
            {
                var missedInSource = diff.Where(d => !sourcePositions.ContainsKey(d)).ToList();
                if (missedInSource.Count > 0)
                {
                    _logger.LogWarning("Next {count} positions are not present in source: {list}", missedInSource.Count, missedInSource.Select(p => new { Id = p, destPositions[p].Name }));
                }

                var missedInDest = diff.Where(d => !destPositions.ContainsKey(d)).ToList();
                if (missedInDest.Count > 0)
                {
                    _logger.LogWarning("Next {count} positions are not present in destination: {list}", missedInDest.Count, missedInDest);
                }
            }
            else
            {
                _logger.LogInformation("All {count} content positions are present both in source and destination", sourcePositions.Count);
            }

            var positionsLinksToClone = sourcePositions.Values
                                                       .Where(p => p.Template != null && destPositions.ContainsKey(p.Id))
                                                       .ToList();
            _logger.LogInformation("There are total {count} positions with links in source that are also present in destination", positionsLinksToClone.Count);

            var clonedCount = 0L;
            var failedIds = new ConcurrentBag<long>();
            await CloneHelpers.ParallelRunAsync(positionsLinksToClone,
                                                _options.MaxDegreeOfParallelism,
                                                async position =>
                                                {
                                                    try
                                                    {
                                                        EnsurePositionsAreEqual(destPositions[position.Id], position);
                                                        await ClonePositionLinkAsync(position, destPositions[position.Id]);
                                                        Interlocked.Increment(ref clonedCount);
                                                        _logger.LogInformation("Position link cloning succeeded: {position}", position);
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        failedIds.Add(position.Id);
                                                        _logger.LogError(new EventId(), ex, "Position link cloning error: {position}", position);
                                                    }
                                                });

            _logger.LogInformation("Cloned position links: {cloned} of {total}", clonedCount, positionsLinksToClone.Count);
            if (failedIds.Count > 0)
            {
                _logger.LogWarning("Id's of failed positions: {list}", failedIds);
                return false;
            }

            return true;
        }

        private static void EnsurePositionsAreEqual(PositionDescriptor destPosition, PositionDescriptor sourcePosition)
        {
            if (sourcePosition.Template == null)
            {
                throw new InvalidOperationException("Source content position has not link with template");
            }

            if (destPosition.Template != null && destPosition.Template.Id != sourcePosition.Template.Id)
            {
                throw new InvalidOperationException($"Positions has different linked templates ({sourcePosition.Template.Id} in source and {destPosition.Template.Id} in destination)");
            }
        }

        private async Task ClonePositionLinkAsync(PositionDescriptor sourcePosition, PositionDescriptor destPosition)
        {
            var templateId = sourcePosition.Template?.Id;
            if (templateId.HasValue)
            {
                if (destPosition?.Template?.Id == templateId)
                {
                    _logger.LogInformation("Link between position {id} and template {template} already exists, skip linking",
                                           sourcePosition.Id,
                                           templateId.Value);
                    return;
                }

                _logger.LogInformation("Creating link between position {id} and template {template}",
                                       sourcePosition.Id,
                                       templateId.Value);
                await DestRestClient.CreatePositionTemplateLinkAsync(sourcePosition.Id.ToString(), templateId.Value.ToString());
            }
        }
    }
}

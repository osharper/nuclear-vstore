using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Events;
using NuClear.VStore.Kafka;
using NuClear.VStore.Objects;
using NuClear.VStore.Options;

namespace NuClear.VStore.Worker.Jobs
{
    public sealed class ObjectEventsProcessingJob : AsyncJob
    {
        private const string VersionsGroupId = "vstore-versions-producer";
        private const string BinariesGroupId = "vstore-binaries-producer";
        private const int BatchSize = 10;

        private readonly string _objectEventsTopic;
        private readonly string _objectVersionsTopic;
        private readonly string _binariesUsingsTopic;

        private readonly ILogger<ObjectEventsProcessingJob> _logger;
        private readonly ObjectsStorageReader _objectsStorageReader;
        private readonly EventSender _eventSender;
        private readonly EventReader _versionEventReader;
        private readonly EventReader _binariesEventReader;

        public ObjectEventsProcessingJob(
            ILogger<ObjectEventsProcessingJob> logger,
            ObjectsStorageReader objectsStorageReader,
            KafkaOptions kafkaOptions,
            EventSender eventSender)
        {
            _objectEventsTopic = kafkaOptions.ObjectEventsTopic;
            _objectVersionsTopic = kafkaOptions.ObjectVersionsTopic;
            _binariesUsingsTopic = kafkaOptions.BinariesUsingsTopic;

            _logger = logger;
            _objectsStorageReader = objectsStorageReader;
            _eventSender = eventSender;
            _versionEventReader = new EventReader(logger, kafkaOptions.BrokerEndpoints, VersionsGroupId);
            _binariesEventReader = new EventReader(logger, kafkaOptions.BrokerEndpoints, BinariesGroupId);
        }

        protected override async Task ExecuteInternalAsync(IReadOnlyDictionary<string, string[]> args, CancellationToken cancellationToken)
        {
            if (args.TryGetValue("mode", out string[] modes))
            {
                var tasks = new List<Task>();
                if (modes.Contains("versions"))
                {
                    var task = Run(
                        async () =>
                            {
                                while (!cancellationToken.IsCancellationRequested)
                                {
                                    try
                                    {
                                        await ProduceObjectVersionCreatedEvents();
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(
                                            new EventId(),
                                            ex,
                                            "[{taskName}] Unexpected error occured: {errorMessage}.",
                                            nameof(ProduceObjectVersionCreatedEvents),
                                            ex.Message);
                                        await Task.Delay(1000, cancellationToken);
                                    }
                                }
                            },
                        cancellationToken);
                    tasks.Add(task);

                    _logger.LogInformation("[{taskName}] task started.", nameof(ProduceObjectVersionCreatedEvents));
                }

                if (modes.Contains("binaries"))
                {
                    var task = Run(
                        async () =>
                            {
                                while (!cancellationToken.IsCancellationRequested)
                                {
                                    try
                                    {
                                        await ProduceBinaryReferencesEvents();
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(
                                            new EventId(),
                                            ex,
                                            "[{taskName}] Unexpected error occured: {errorMessage}.",
                                            nameof(ProduceBinaryReferencesEvents),
                                            ex.Message);
                                        await Task.Delay(1000, cancellationToken);
                                    }
                                }
                            },
                        cancellationToken);
                    tasks.Add(task);

                    _logger.LogInformation("[{taskName}] task started.", nameof(ProduceBinaryReferencesEvents));
                }

                await Task.WhenAll(tasks);
            }
            else
            {
                throw new ArgumentException("Mode argument not specified.");
            }
        }

        private static async Task Run(Func<Task> task, CancellationToken cancellationToken) =>
            await Task.Factory.StartNew(
                          async () => await task(),
                          cancellationToken,
                          TaskCreationOptions.LongRunning,
                          TaskScheduler.Default)
                      .Unwrap();

        private async Task ProduceObjectVersionCreatedEvents()
        {
            var events = _versionEventReader.Read<ObjectVersionCreatingEvent>(_objectEventsTopic, BatchSize);
            foreach (var @event in events)
            {
                var objectId = @event.Source.ObjectId;
                var versionId = @event.Source.CurrentVersionId;
                var versions = await _objectsStorageReader.GetObjectVersions(objectId, versionId);
                _logger.LogInformation(
                    "[{taskName}] There are '{versionsCount}' new versions were created after the versionId = '{versionId}' for the object id = '{objectId}'.",
                    nameof(ProduceObjectVersionCreatedEvents),
                    versions.Count,
                    versionId,
                    objectId);
                foreach (var descriptor in versions)
                {
                    await _eventSender.SendAsync(_objectVersionsTopic, new ObjectVersionCreatedEvent(descriptor.Id, descriptor.VersionId));
                    _logger.LogInformation(
                        "[{taskName}] Event for object id = '{objectId}' and versionId = {versionId}' sent to '{topic}'.",
                        nameof(ProduceObjectVersionCreatedEvents),
                        descriptor.Id, 
                        descriptor.VersionId,
                        _objectVersionsTopic);
                }
            }

            await _versionEventReader.CommitAsync(events.Select(x => x.TopicPartitionOffset));
        }

        private async Task ProduceBinaryReferencesEvents()
        {
            var events = _binariesEventReader.Read<ObjectVersionCreatingEvent>(_objectEventsTopic, BatchSize);
            foreach (var @event in events)
            {
                var objectId = @event.Source.ObjectId;
                var versionId = @event.Source.CurrentVersionId;
                var versions = await _objectsStorageReader.GetObjectVersions(objectId, versionId);
                _logger.LogInformation(
                    "[{taskName}] There are '{versionsCount}' new versions were created after the versionId = '{versionId}' " +
                    "for the object id = '{objectId}'.",
                    nameof(ProduceBinaryReferencesEvents),
                    versions.Count,
                    versionId,
                    objectId);
                foreach (var descriptor in versions)
                {
                    var objectDescriptor = await _objectsStorageReader.GetObjectDescriptor(objectId, descriptor.VersionId);
                    var fileInfos = objectDescriptor.Elements
                                                    .Where(x => x.Value is IBinaryElementValue)
                                                    .Select(x => (TemplateCode: x.TemplateCode, FileKey: ((IBinaryElementValue)x.Value).Raw))
                                                    .ToList();
                    foreach (var fileInfo in fileInfos)
                    {
                        await _eventSender.SendAsync(
                            _binariesUsingsTopic,
                            new BinaryUsedEvent(objectId, descriptor.VersionId, fileInfo.TemplateCode, fileInfo.FileKey));
                        _logger.LogInformation(
                            "[{taskName}] Event for binary reference '{fileKey}' for element with templateCode = '{templateCode}' " +
                            "for object id = '{objectId}' and versionId = {versionId}' sent to '{topic}'.",
                            nameof(ProduceBinaryReferencesEvents),
                            fileInfo.FileKey,
                            fileInfo.TemplateCode,
                            descriptor.Id,
                            descriptor.VersionId,
                            _binariesUsingsTopic);
                    }
                }
            }

            await _binariesEventReader.CommitAsync(events.Select(x => x.TopicPartitionOffset));
        }
    }
}
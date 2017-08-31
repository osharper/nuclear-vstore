using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuClear.VStore.DataContract;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Events;
using NuClear.VStore.Kafka;
using NuClear.VStore.Objects;
using NuClear.VStore.Options;
using NuClear.VStore.S3;

using Polly;
using Polly.Wrap;

namespace NuClear.VStore.Worker.Jobs
{
    public sealed class ObjectEventsProcessingJob : AsyncJob
    {
        private const string VersionsGroupId = "vstore-versions-producer";
        private const string BinariesGroupId = "vstore-binaries-producer";

        private readonly int _consumingBatchSize;
        private readonly string _objectEventsTopic;
        private readonly string _objectVersionsTopic;
        private readonly string _binariesUsingsTopic;

        private readonly ILogger<ObjectEventsProcessingJob> _logger;
        private readonly ObjectsStorageReader _objectsStorageReader;
        private readonly EventSender _eventSender;
        private readonly EventReceiver _versionEventReceiver;
        private readonly EventReceiver _binariesEventReceiver;

        public ObjectEventsProcessingJob(
            string environment,
            ILogger<ObjectEventsProcessingJob> logger,
            ObjectsStorageReader objectsStorageReader,
            KafkaOptions kafkaOptions,
            EventSender eventSender)
        {
            _consumingBatchSize = kafkaOptions.ConsumingBatchSize;
            _objectEventsTopic = kafkaOptions.ObjectEventsTopic;
            _objectVersionsTopic = kafkaOptions.ObjectVersionsTopic;
            _binariesUsingsTopic = kafkaOptions.BinariesReferencesTopic;

            _logger = logger;
            _objectsStorageReader = objectsStorageReader;
            _eventSender = eventSender;
            _versionEventReceiver = new EventReceiver(logger, kafkaOptions.BrokerEndpoints, $"{VersionsGroupId}-{environment}");
            _binariesEventReceiver = new EventReceiver(logger, kafkaOptions.BrokerEndpoints, $"{BinariesGroupId}-{environment}");
        }

        protected override async Task ExecuteInternalAsync(IReadOnlyDictionary<string, string[]> args, CancellationToken cancellationToken)
        {
            if (args.TryGetValue(CommandLine.Arguments.Mode, out var modes))
            {
                var tasks = new List<Task>();
                if (modes.Contains(CommandLine.ArgumentValues.Versions))
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
                                    catch (ObjectNotFoundException ex)
                                    {
                                        _logger.LogWarning(
                                            "{taskName}: Got an event for the non-existing object. Message: {errorMessage}. The event will be processed again.",
                                            nameof(ProduceObjectVersionCreatedEvents),
                                            ex.Message);
                                        await Task.Delay(1000, cancellationToken);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(
                                            new EventId(),
                                            ex,
                                            "{taskName}: Unexpected error occured: {errorMessage}.",
                                            nameof(ProduceObjectVersionCreatedEvents),
                                            ex.Message);
                                        await Task.Delay(1000, cancellationToken);
                                    }
                                }
                            },
                        cancellationToken);
                    tasks.Add(task);

                    _logger.LogInformation("{taskName} task started.", nameof(ProduceObjectVersionCreatedEvents));
                }

                if (modes.Contains(CommandLine.ArgumentValues.Binaries))
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
                                    catch (ObjectNotFoundException ex)
                                    {
                                        _logger.LogWarning(
                                            "{taskName}: Got an event for the non-existing object. Message: {errorMessage}. The event will be processed again.",
                                            nameof(ProduceObjectVersionCreatedEvents),
                                            ex.Message);
                                        await Task.Delay(1000, cancellationToken);
                                    }
                                    catch (Exception ex)
                                    {
                                        _logger.LogError(
                                            new EventId(),
                                            ex,
                                            "{taskName}: Unexpected error occured: {errorMessage}.",
                                            nameof(ProduceBinaryReferencesEvents),
                                            ex.Message);
                                        await Task.Delay(1000, cancellationToken);
                                    }
                                }
                            },
                        cancellationToken);
                    tasks.Add(task);

                    _logger.LogInformation("{taskName} task started.", nameof(ProduceBinaryReferencesEvents));
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

        private static PolicyWrap<IReadOnlyCollection<ObjectVersionRecord>> CreateGetObjectVersionsResiliencePolicy()
        {
            var fallback =
                Policy<IReadOnlyCollection<ObjectVersionRecord>>
                    .Handle<ObjectNotFoundException>()
                    .FallbackAsync((IReadOnlyCollection<ObjectVersionRecord>)null);
            var retry =
                Policy<IReadOnlyCollection<ObjectVersionRecord>>
                    .Handle<ObjectNotFoundException>()
                    .WaitAndRetryAsync(5, attempt => TimeSpan.FromSeconds(1));
            return Policy.WrapAsync(fallback, retry);
        }

        private async Task ProduceObjectVersionCreatedEvents()
        {
            var events = _versionEventReceiver.Receive<ObjectVersionCreatingEvent>(_objectEventsTopic, _consumingBatchSize);
            foreach (var @event in events)
            {
                var objectId = @event.Source.ObjectId;
                var versionId = @event.Source.CurrentVersionId;

                IReadOnlyCollection<ObjectVersionRecord> versionRecords;
                if (string.IsNullOrEmpty(versionId))
                {
                    var policy = CreateGetObjectVersionsResiliencePolicy();
                    versionRecords = await policy.ExecuteAsync(async () => await _objectsStorageReader.GetObjectVersions(objectId, versionId));
                    if (versionRecords == null)
                    {
                        _logger.LogWarning(
                            "{taskName}: Got an event for the object with id = '{objectId}' that was not eventually created. The event will be skipped.",
                            nameof(ProduceBinaryReferencesEvents),
                            objectId);
                        continue;
                    }
                }
                else
                {
                    versionRecords = await _objectsStorageReader.GetObjectVersions(objectId, versionId);
                }

                _logger.LogInformation(
                    "{taskName}: There are '{versionsCount}' new versions were created after the versionId = {versionId} for the object id = '{objectId}'.",
                    nameof(ProduceObjectVersionCreatedEvents),
                    versionRecords.Count,
                    versionId,
                    objectId);
                foreach (var record in versionRecords)
                {
                    var versionCreatedEvent = new ObjectVersionCreatedEvent(
                        record.Id,
                        record.VersionId,
                        record.VersionIndex,
                        record.Author,
                        record.Properties,
                        record.LastModified);
                    await _eventSender.SendAsync(_objectVersionsTopic, versionCreatedEvent);

                    _logger.LogInformation(
                        "{taskName}: Event for object id = '{objectId}' and versionId = {versionId} sent to {topic}.",
                        nameof(ProduceObjectVersionCreatedEvents),
                        record.Id,
                        record.VersionId,
                        _objectVersionsTopic);
                }
            }

            await _versionEventReceiver.CommitAsync(events.Select(x => x.TopicPartitionOffset));
        }

        private async Task ProduceBinaryReferencesEvents()
        {
            var events = _binariesEventReceiver.Receive<ObjectVersionCreatingEvent>(_objectEventsTopic, _consumingBatchSize);
            foreach (var @event in events)
            {
                var objectId = @event.Source.ObjectId;
                var versionId = @event.Source.CurrentVersionId;

                IReadOnlyCollection<ObjectVersionRecord> versionRecords;
                if (string.IsNullOrEmpty(versionId))
                {
                    var policy = CreateGetObjectVersionsResiliencePolicy();
                    versionRecords = await policy.ExecuteAsync(async () => await _objectsStorageReader.GetObjectVersions(objectId, versionId));
                    if (versionRecords == null)
                    {
                        _logger.LogWarning(
                            "{taskName}: Got an event for the object with id = '{objectId}' that was not eventually created. The event will be skipped.",
                            nameof(ProduceBinaryReferencesEvents),
                            objectId);
                        continue;
                    }
                }
                else
                {
                    versionRecords = await _objectsStorageReader.GetObjectVersions(objectId, versionId);
                }

                _logger.LogInformation(
                    "{taskName}: There are '{versionsCount}' new versions were created after the versionId = {versionId} " +
                    "for the object id = '{objectId}'.",
                    nameof(ProduceBinaryReferencesEvents),
                    versionRecords.Count,
                    versionId,
                    objectId);
                foreach (var record in versionRecords)
                {
                    var fileInfos = record.Elements
                                          .Where(x => x.Value is IBinaryElementValue binaryValue && !string.IsNullOrEmpty(binaryValue.Raw))
                                          .Select(x => (TemplateCode: x.TemplateCode, FileKey: ((IBinaryElementValue)x.Value).Raw))
                                          .ToList();
                    foreach (var fileInfo in fileInfos)
                    {
                        await _eventSender.SendAsync(
                            _binariesUsingsTopic,
                            new BinaryReferencedEvent(objectId, record.VersionId, fileInfo.TemplateCode, fileInfo.FileKey));
                        _logger.LogInformation(
                            "{taskName}: Event for binary reference {fileKey} for element with templateCode = '{templateCode}' " +
                            "for object id = '{objectId}' and versionId = {versionId} sent to {topic}.",
                            nameof(ProduceBinaryReferencesEvents),
                            fileInfo.FileKey,
                            fileInfo.TemplateCode,
                            record.Id,
                            record.VersionId,
                            _binariesUsingsTopic);
                    }
                }
            }

            await _binariesEventReceiver.CommitAsync(events.Select(x => x.TopicPartitionOffset));
        }
    }
}
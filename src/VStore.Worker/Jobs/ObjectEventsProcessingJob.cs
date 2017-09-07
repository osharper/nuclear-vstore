using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
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
using Polly.Retry;
using Polly.Wrap;

namespace NuClear.VStore.Worker.Jobs
{
    public sealed class ObjectEventsProcessingJob : AsyncJob, IDisposable
    {
        private const string VersionsGroupId = "vstore-versions-producer";
        private const string BinariesGroupId = "vstore-binaries-producer";

        private readonly string _objectVersionsTopic;
        private readonly string _binariesUsingsTopic;

        private readonly ILogger<ObjectEventsProcessingJob> _logger;
        private readonly ObjectsStorageReader _objectsStorageReader;
        private readonly EventSender _eventSender;
        private readonly EventReceiver _versionEventReceiver;
        private readonly EventReceiver _binariesEventReceiver;

        private CancellationTokenRegistration _cancellationRegistration;

        public ObjectEventsProcessingJob(
            ILogger<ObjectEventsProcessingJob> logger,
            ObjectsStorageReader objectsStorageReader,
            KafkaOptions kafkaOptions,
            EventSender eventSender)
        {
            _objectVersionsTopic = kafkaOptions.ObjectVersionsTopic;
            _binariesUsingsTopic = kafkaOptions.BinariesReferencesTopic;

            _logger = logger;
            _objectsStorageReader = objectsStorageReader;
            _eventSender = eventSender;
            _versionEventReceiver = new EventReceiver(
                logger,
                kafkaOptions.BrokerEndpoints,
                $"{VersionsGroupId}-{kafkaOptions.ConsumerGroupToken}",
                new[] { kafkaOptions.ObjectEventsTopic });
            _binariesEventReceiver = new EventReceiver(
                logger,
                kafkaOptions.BrokerEndpoints,
                $"{BinariesGroupId}-{kafkaOptions.ConsumerGroupToken}",
                new[] { kafkaOptions.ObjectEventsTopic });
        }

        public void Dispose()
        {
            _cancellationRegistration.Dispose();
            _versionEventReceiver.Dispose();
            _binariesEventReceiver.Dispose();
        }

        protected override async Task ExecuteInternalAsync(IReadOnlyDictionary<string, string[]> args, CancellationToken cancellationToken)
        {
            var subscriptions = new List<IDisposable>();
            if (args.TryGetValue(CommandLine.Arguments.Mode, out var modes))
            {
                if (modes.Contains(CommandLine.ArgumentValues.Versions))
                {
                    var subscription = ObjectVersionCreatedEventsProducing(cancellationToken);
                    subscriptions.Add(subscription);
                    _logger.LogInformation("{taskName} task started.", nameof(ObjectVersionCreatedEventsProducing));
                }

                if (modes.Contains(CommandLine.ArgumentValues.Binaries))
                {
                    var subscription = BinaryReferenceEventsProducing(cancellationToken);
                    subscriptions.Add(subscription);
                    _logger.LogInformation("{taskName} task started.", nameof(BinaryReferenceEventsProducing));
                }
            }
            else
            {
                throw new ArgumentException("Mode argument not specified.");
            }

            var taskCompletionSource = new TaskCompletionSource<object>();
            _cancellationRegistration = cancellationToken.Register(
                () =>
                    {
                        subscriptions.ForEach(x => x.Dispose());
                        taskCompletionSource.TrySetResult(null);
                    });

            await taskCompletionSource.Task;
        }

        private static RetryPolicy CreateRetryPolicy(ILogger logger, string taskName)
            => Policy.Handle<Exception>()
                     .WaitAndRetryForever(
                         attempt => TimeSpan.FromSeconds(1),
                         (ex, duration) =>
                             {
                                 if (ex is ObjectNotFoundException)
                                 {
                                     logger.LogWarning(
                                         "{taskName}: Got an event for the non-existing object. Message: {errorMessage}. The event will be processed again.",
                                         taskName,
                                         ex.Message);
                                 }
                                 else
                                 {
                                     logger.LogError(
                                         new EventId(),
                                         ex,
                                         "{taskName}: Unexpected error occured: {errorMessage}.",
                                         taskName,
                                         ex.Message);
                                 }
                             });

        private static PolicyWrap<IReadOnlyCollection<ObjectVersionRecord>> CreateGetObjectVersionsResiliencePolicy()
        {
            var fallback =
                Policy<IReadOnlyCollection<ObjectVersionRecord>>
                    .Handle<ObjectNotFoundException>()
                    .FallbackAsync((IReadOnlyCollection<ObjectVersionRecord>)null);
            var retry =
                Policy<IReadOnlyCollection<ObjectVersionRecord>>
                    .Handle<ObjectNotFoundException>()
                    .WaitAndRetryAsync(300, attempt => TimeSpan.FromSeconds(1));
            return Policy.WrapAsync(fallback, retry);
        }

        private IDisposable ObjectVersionCreatedEventsProducing(CancellationToken cancellationToken)
        {
            async Task ProcessAsync(KafkaEvent<ObjectVersionCreatingEvent> @event)
            {
                var objectId = @event.Source.ObjectId;
                var versionId = @event.Source.CurrentVersionId;

                IReadOnlyCollection<ObjectVersionRecord> versionRecords;
                if (string.IsNullOrEmpty(versionId))
                {
                    var policy = CreateGetObjectVersionsResiliencePolicy();
                    versionRecords = await policy.ExecuteAsync(() => _objectsStorageReader.GetObjectVersions(objectId, versionId));
                    if (versionRecords == null)
                    {
                        _logger.LogWarning(
                            "{taskName}: Got an event for the object with id = '{objectId}' that was not eventually created. The event will be skipped.",
                            nameof(ObjectVersionCreatedEventsProducing),
                            objectId);
                        return;
                    }
                }
                else
                {
                    versionRecords = await _objectsStorageReader.GetObjectVersions(objectId, versionId);
                }

                _logger.LogInformation(
                    "{taskName}: There are '{versionsCount}' new versions were created after the versionId = {versionId} for the object id = '{objectId}'.",
                    nameof(ObjectVersionCreatedEventsProducing),
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
                        nameof(ObjectVersionCreatedEventsProducing),
                        record.Id,
                        record.VersionId,
                        _objectVersionsTopic);
                }

                await _versionEventReceiver.CommitAsync(@event);
            }

            var observable = _versionEventReceiver.Subscribe<ObjectVersionCreatingEvent>(cancellationToken);
            return observable
                .Do(@event =>
                        {
                            var retry = CreateRetryPolicy(_logger, nameof(ObjectVersionCreatedEventsProducing));
                            retry.Execute(() => ProcessAsync(@event).GetAwaiter().GetResult());
                        })
                .Subscribe();
        }

        private IDisposable BinaryReferenceEventsProducing(CancellationToken cancellationToken)
        {
            async Task ProcessAsync(KafkaEvent<ObjectVersionCreatingEvent> @event)
            {
                var objectId = @event.Source.ObjectId;
                var versionId = @event.Source.CurrentVersionId;

                IReadOnlyCollection<ObjectVersionRecord> versionRecords;
                if (string.IsNullOrEmpty(versionId))
                {
                    var policy = CreateGetObjectVersionsResiliencePolicy();
                    versionRecords = await policy.ExecuteAsync(() => _objectsStorageReader.GetObjectVersions(objectId, versionId));
                    if (versionRecords == null)
                    {
                        _logger.LogWarning(
                            "{taskName}: Got an event for the object with id = '{objectId}' that was not eventually created. The event will be skipped.",
                            nameof(ObjectVersionCreatedEventsProducing),
                            objectId);
                        return;
                    }
                }
                else
                {
                    versionRecords = await _objectsStorageReader.GetObjectVersions(objectId, versionId);
                }

                _logger.LogInformation(
                    "{taskName}: There are '{versionsCount}' new versions were created after the versionId = {versionId} " +
                    "for the object id = '{objectId}'.",
                    nameof(BinaryReferenceEventsProducing),
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
                            new BinaryReferencedEvent(objectId, record.VersionId, fileInfo.TemplateCode, fileInfo.FileKey, record.LastModified));
                        _logger.LogInformation(
                            "{taskName}: Event for binary reference {fileKey} for element with templateCode = '{templateCode}' " +
                            "for object id = '{objectId}' and versionId = {versionId} sent to {topic}.",
                            nameof(BinaryReferenceEventsProducing),
                            fileInfo.FileKey,
                            fileInfo.TemplateCode,
                            record.Id,
                            record.VersionId,
                            _binariesUsingsTopic);
                    }
                }

                await _binariesEventReceiver.CommitAsync(@event);
            }

            var observable = _binariesEventReceiver.Subscribe<ObjectVersionCreatingEvent>(cancellationToken);
            return observable
                .Do(@event =>
                        {
                            var retry = CreateRetryPolicy(_logger, nameof(BinaryReferenceEventsProducing));
                            retry.Execute(() => ProcessAsync(@event).GetAwaiter().GetResult());
                        })
                .Subscribe();
        }
    }
}
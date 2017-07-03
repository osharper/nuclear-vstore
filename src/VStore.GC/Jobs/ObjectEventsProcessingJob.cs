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
            _versionEventReader = new EventReader(logger, kafkaOptions.BrokerEndpoints, BinariesGroupId);
        }

        protected override async Task ExecuteInternalAsync(IReadOnlyDictionary<string, string[]> args, CancellationToken cancellationToken)
        {
            if (args.TryGetValue("mode", out string[] modes))
            {
                if (modes.Contains("versions"))
                {
                    await Task.Run(
                        async () =>
                        {
                            while (!cancellationToken.IsCancellationRequested)
                            {
                                await ProduceObjectVersionCreatedEvents();
                            }
                        },
                        cancellationToken);
                }

                if (modes.Contains("binaries"))
                {
                    await Task.Run(
                        async () =>
                        {
                            while (!cancellationToken.IsCancellationRequested)
                            {
                                await ProduceBinaryUsedEvents();
                            }
                        },
                        cancellationToken);
                }
            }
            else
            {
                throw new ArgumentException("Mode argument not specified.");
            }
        }

        private async Task ProduceObjectVersionCreatedEvents()
        {
            var events = _versionEventReader.Read<ObjectVersionCreatingEvent>(_objectEventsTopic, BatchSize);
            foreach (var @event in events)
            {
                var versions = await _objectsStorageReader.GetObjectVersions(@event.Source.ObjectId, @event.Source.CurrentVersionId);
                foreach (var descriptor in versions)
                {
                    await _eventSender.SendAsync(_objectVersionsTopic, new ObjectVersionCreatedEvent(descriptor.Id, descriptor.VersionId));
                }
            }

            await _versionEventReader.CommitAsync(events.Select(x => x.TopicPartitionOffset));
        }

        private async Task ProduceBinaryUsedEvents()
        {
            var events = _binariesEventReader.Read<ObjectVersionCreatingEvent>(_objectEventsTopic, BatchSize);
            foreach (var @event in events)
            {
                var objectId = @event.Source.ObjectId;
                var versions = await _objectsStorageReader.GetObjectVersions(objectId, @event.Source.CurrentVersionId);
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
                    }
                }
            }

            await _versionEventReader.CommitAsync(events.Select(x => x.TopicPartitionOffset));
        }
    }
}
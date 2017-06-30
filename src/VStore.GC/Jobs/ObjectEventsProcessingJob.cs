using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Events;
using NuClear.VStore.Kafka;
using NuClear.VStore.Objects;
using NuClear.VStore.Options;

namespace NuClear.VStore.Worker.Jobs
{
    public sealed class ObjectEventsProcessingJob : AsyncJob
    {
        private const string GroupId = "vstore-versions-producer";
        private const int BatchSize = 10;

        private readonly string _objectEventsTopic;
        private readonly string _objectVersionsTopic;

        private readonly ILogger<ObjectEventsProcessingJob> _logger;
        private readonly ObjectsStorageReader _objectsStorageReader;
        private readonly EventSender _eventSender;
        private readonly EventReader _eventReader;

        public ObjectEventsProcessingJob(
            ILogger<ObjectEventsProcessingJob> logger,
            ObjectsStorageReader objectsStorageReader,
            KafkaOptions kafkaOptions,
            EventSender eventSender)
        {
            _objectEventsTopic = kafkaOptions.ObjectEventsTopic;
            _objectVersionsTopic = kafkaOptions.ObjectVersionsTopic;

            _logger = logger;
            _objectsStorageReader = objectsStorageReader;
            _eventSender = eventSender;
            _eventReader = new EventReader(logger, kafkaOptions.BrokerEndpoints, GroupId);
        }

        protected override async Task ExecuteInternalAsync(CancellationToken cancellationToken)
        {
            var events = _eventReader.Read<ObjectVersionCreatingEvent>(_objectEventsTopic, BatchSize);
            foreach (var @event in events)
            {
                var versions = await _objectsStorageReader.GetAllObjectRootVersions(@event.Source.ObjectId);

                var currentVersionReached = false;
                foreach (var descriptor in versions)
                {
                    if (!currentVersionReached)
                    {
                        currentVersionReached = descriptor.VersionId.Equals(@event.Source.CurrentVersionId, StringComparison.OrdinalIgnoreCase);
                        continue;
                    }

                    await _eventSender.SendAsync(_objectVersionsTopic, new ObjectVersionCreatedEvent(descriptor.Id, descriptor.VersionId));
                }
            }

            await _eventReader.CommitAsync(events.Select(x => x.TopicPartitionOffset));
        }
    }
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Events;
using NuClear.VStore.Kafka;
using NuClear.VStore.Options;
using NuClear.VStore.Sessions;

namespace NuClear.VStore.Worker.Jobs
{
    public class BinariesCleanupJob : AsyncJob
    {
        private const string GroupId = "vstore-sessions-cleaner";
        private const char SlashChar = '/';

        private readonly int _consumingBatchSize;
        private readonly string _sessionsTopicName;
        private readonly string _binariesReferencesTopicName;

        private readonly ILogger<BinariesCleanupJob> _logger;
        private readonly SessionCleanupService _sessionCleanupService;
        private readonly EventReceiver _eventReceiver;

        public BinariesCleanupJob(
            ILogger<BinariesCleanupJob> logger,
            KafkaOptions kafkaOptions,
            SessionCleanupService sessionCleanupService)
        {
            _sessionsTopicName = kafkaOptions.SessionEventsTopic;
            _binariesReferencesTopicName = kafkaOptions.BinariesReferencesTopic;
            _consumingBatchSize = kafkaOptions.ConsumingBatchSize;

            _logger = logger;
            _sessionCleanupService = sessionCleanupService;
            _eventReceiver = new EventReceiver(logger, kafkaOptions.BrokerEndpoints, GroupId);
        }

        protected override async Task ExecuteInternalAsync(IReadOnlyDictionary<string, string[]> args, CancellationToken cancellationToken)
        {
            if (!args.TryGetValue(CommandLine.Arguments.Range, out var value) || value.Length == 0)
            {
                throw new ArgumentException("Range argument not specified.");
            }

            if (!TimeSpan.TryParse(value[0], out var range))
            {
                throw new ArgumentException("Range argument format is incorrect. Make sure that it's specified like 'd:hh:mm:ss'.");
            }

            Guid EvaluateSessionId(string fileKey) => new Guid(fileKey.Substring(0, fileKey.IndexOf(SlashChar)));

            var utcNow = DateTime.UtcNow;
            var referenceEvents = _eventReceiver.Receive<BinaryReferencedEvent>(_binariesReferencesTopicName, utcNow.Subtract(range));
            var sessionsWithReferences = new HashSet<Guid>(referenceEvents.Select(x => EvaluateSessionId(x.Source.FileKey)));

            await RemoveUnreferencedBinaries(utcNow, sessionsWithReferences);
        }

        private async Task RemoveUnreferencedBinaries(DateTime currentTime, ICollection<Guid> sessionsWithReferences)
        {
            while (true)
            {
                var sessionCreatingEvents = _eventReceiver.Receive<SessionCreatingEvent>(_sessionsTopicName, _consumingBatchSize);
                var expiredSessionEvents = sessionCreatingEvents.Where(x => x.Source.ExpiresAt <= currentTime).ToList();

                if (expiredSessionEvents.Count == 0)
                {
                    return;
                }

                foreach (var expiredSessionEvent in expiredSessionEvents)
                {
                    var sessionId = expiredSessionEvent.Source.SessionId;
                    if (sessionsWithReferences.Contains(sessionId))
                    {
                        continue;
                    }

                    var success = await _sessionCleanupService.DeleteSessionAsync(sessionId);
                    if (success)
                    {
                        _logger.LogInformation("Session '{sessionId}' with all uploaded files deleted as unreferenced and already expired.", sessionId);
                    }

                    await _eventReceiver.CommitAsync(new[] { expiredSessionEvent.TopicPartitionOffset });
                    _logger.LogInformation(
                        "Event of type '{eventType}' for expired session '{sessionId}' processed.",
                        expiredSessionEvent.Source.GetType(),
                        sessionId);
                }
            }
        }
    }
}
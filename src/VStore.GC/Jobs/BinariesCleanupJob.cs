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

namespace NuClear.VStore.GC.Jobs
{
    public class BinariesCleanupJob : AsyncJob
    {
        private const int BatchSize = 100;
        private const char SlashChar = '/';

        private readonly ILogger<BinariesCleanupJob> _logger;
        private readonly string _sessionsTopicName;
        private readonly string _binariesUsingsTopicName;
        private readonly EventReader _eventReader;
        private readonly SessionCleanupService _sessionCleanupService;

        public BinariesCleanupJob(
            ILogger<BinariesCleanupJob> logger,
            KafkaOptions kafkaOptions,
            EventReader eventReader,
            SessionCleanupService sessionCleanupService)
        {
            _logger = logger;
            _sessionsTopicName = kafkaOptions.SessionsTopic;
            _binariesUsingsTopicName = kafkaOptions.BinariesUsingsTopic;
            _eventReader = eventReader;
            _sessionCleanupService = sessionCleanupService;
        }

        protected override async Task ExecuteInternalAsync(CancellationToken cancellationToken)
        {
            var utcNow = DateTime.UtcNow;
            var expiredSessionsProcessed = false;

            while (!expiredSessionsProcessed && !cancellationToken.IsCancellationRequested)
            {
                var sessionCreatedEvents = _eventReader.Read<SessionCreatedEvent>(_sessionsTopicName, BatchSize);
                var expiredSessionEvents = sessionCreatedEvents.Where(x => x.Source.ExpiresAt <= utcNow).ToList();
                expiredSessionsProcessed = sessionCreatedEvents.Count > expiredSessionEvents.Count;

                var yesterdayUsingEvents = _eventReader.ReadPartition<BinaryUsedEvent>(_binariesUsingsTopicName, utcNow.DayOfYear - 1);
                var todayUsingEvents = _eventReader.ReadPartition<BinaryUsedEvent>(_binariesUsingsTopicName, utcNow.DayOfYear);

                Guid EvaluateSessionId(string fileKey) => new Guid(fileKey.Substring(0, fileKey.IndexOf(SlashChar)));
                var usedBinariesSessions = new HashSet<Guid>(yesterdayUsingEvents.Concat(todayUsingEvents).Select(x => EvaluateSessionId(x.FileKey)));

                foreach (var expiredSessionEvent in expiredSessionEvents)
                {
                    var sessionId = expiredSessionEvent.Source.SessionId;
                    if (!usedBinariesSessions.Contains(sessionId))
                    {
                        var success = await _sessionCleanupService.DeleteSessionAsync(sessionId);
                        if (success)
                        {
                            _logger.LogInformation("Session '{sessionId}' with all uploaded files deleted as unused and already expired.", sessionId);
                        }
                        else
                        {
                            _logger.LogWarning("Tried to delete non-existent session '{sessionId}'.", sessionId);
                            if (sessionCreatedEvents.Count == 1 && expiredSessionEvents.Count == 1)
                            {
                                return;
                            }
                        }

                        await _eventReader.CommitAsync(new[] { expiredSessionEvent.TopicPartitionOffset });
                        _logger.LogInformation(
                            "Event of type '{eventType}' for expired session '{sessionId}' commited.",
                            expiredSessionEvent.Source.GetType(),
                            sessionId);
                    }
                }
            }
        }
    }
}
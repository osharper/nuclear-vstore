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

        private const int BatchSize = 100;
        private const char SlashChar = '/';

        private readonly ILogger<BinariesCleanupJob> _logger;
        private readonly string _sessionsTopicName;
        private readonly string _binariesUsingsTopicName;
        private readonly SessionCleanupService _sessionCleanupService;
        private readonly EventReader _eventReader;

        public BinariesCleanupJob(
            ILogger<BinariesCleanupJob> logger,
            KafkaOptions kafkaOptions,
            SessionCleanupService sessionCleanupService)
        {
            _logger = logger;
            _sessionsTopicName = kafkaOptions.SessionsTopic;
            _binariesUsingsTopicName = kafkaOptions.BinariesUsingsTopic;
            _sessionCleanupService = sessionCleanupService;
            _eventReader = new EventReader(logger, kafkaOptions.BrokerEndpoints, GroupId);
        }

        protected override async Task ExecuteInternalAsync(IReadOnlyDictionary<string, string[]> args, CancellationToken cancellationToken)
        {
            if (!args.TryGetValue(CommandLine.Arguments.Range, out var value) && value.Length == 0)
            {
                throw new ArgumentException("Range argument not specified.");
            }

            if (!TimeSpan.TryParse(value[0], out var range))
            {
                throw new ArgumentException("Range argument format is incorrect. Make sure that it's specified like 'd:hh:mm:ss'.");
            }

            var utcNow = DateTime.UtcNow;
            while (!cancellationToken.IsCancellationRequested)
            {
                var sessionCreatedEvents = _eventReader.Read<SessionCreatingEvent>(_sessionsTopicName, BatchSize);
                var expiredSessionEvents = sessionCreatedEvents.Where(x => x.Source.ExpiresAt <= utcNow).ToList();

                var referenceEvents = _eventReader.Read<BinaryUsedEvent>(_binariesUsingsTopicName, utcNow.Subtract(range), BatchSize);

                Guid EvaluateSessionId(string fileKey) => new Guid(fileKey.Substring(0, fileKey.IndexOf(SlashChar)));
                var referencedBinariesSessions = new HashSet<Guid>(referenceEvents.Select(x => EvaluateSessionId(x.Source.FileKey)));

                foreach (var expiredSessionEvent in expiredSessionEvents)
                {
                    var sessionId = expiredSessionEvent.Source.SessionId;
                    if (!referencedBinariesSessions.Contains(sessionId))
                    {
                        var success = await _sessionCleanupService.DeleteSessionAsync(sessionId);
                        if (success)
                        {
                            _logger.LogInformation("Session '{sessionId}' with all uploaded files deleted as unused and already expired.", sessionId);
                        }

                        await _eventReader.CommitAsync(new[] { expiredSessionEvent.TopicPartitionOffset });
                        _logger.LogInformation(
                            "Event of type '{eventType}' for expired session '{sessionId}' processed.",
                            expiredSessionEvent.Source.GetType(),
                            sessionId);
                    }
                }
            }
        }
    }
}
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

        private static readonly TimeSpan FailDelay = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan SafetyPeriod = TimeSpan.FromMinutes(5);

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
            var (range, delay) = ParseArgs(args);
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await CleanupBinaries(range, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        new EventId(),
                        ex,
                        "Unexpected error occured: {errorMessage}. The '{workerJobType}' will retry in {delay}.",
                        ex.Message,
                        nameof(BinariesCleanupJob),
                        FailDelay);

                    await Task.Delay(FailDelay, cancellationToken);
                    continue;
                }

                await Task.Delay(delay, cancellationToken);
            }
        }

        private async Task CleanupBinaries(TimeSpan range, CancellationToken cancellationToken)
        {
            var sessionCreatingEvents = _eventReceiver.Receive<SessionCreatingEvent>(_sessionsTopicName, 1);
            if (sessionCreatingEvents.Count == 0)
            {
                _logger.LogInformation(
                    "There are no events of type '{eventType}'. The '{workerJobType}' will now wait.",
                    typeof(SessionCreatingEvent).Name,
                    typeof(BinariesCleanupJob).Name);
                return;
            }

            Guid EvaluateSessionId(string fileKey) => new Guid(fileKey.Substring(0, fileKey.IndexOf(SlashChar)));
            var oldestSessionDate = sessionCreatingEvents.First().Timestamp.UtcDateTime;

            var dateToStart = DateTime.UtcNow.Subtract(range);
            dateToStart = new DateTime(Math.Min(dateToStart.Ticks, oldestSessionDate.Subtract(SafetyPeriod).Ticks));

            var referenceEvents = _eventReceiver.Receive<BinaryReferencedEvent>(_binariesReferencesTopicName, dateToStart);
            if (referenceEvents.Count > 0)
            {
                var periodEnd = referenceEvents.Last().Timestamp;
                var sessionsWithReferences = new HashSet<Guid>(referenceEvents.Select(x => EvaluateSessionId(x.Source.FileKey)));

                var result = await RemoveUnreferencedBinaries(periodEnd.UtcDateTime, sessionsWithReferences, cancellationToken);
                _logger.LogInformation(
                    "Total {totalSessionsCount} sessions has been processed ({expiredSessionsCount} were expired by {periodEnd}). The '{workerJobType}' will now wait.",
                    result.totalSessionsCount,
                    result.expiredSessionsCount,
                    periodEnd.UtcDateTime,
                    typeof(BinariesCleanupJob).Name);
            }
            else
            {
                _logger.LogWarning(
                    "There are no events of type '{eventType}' in time range '{range}' in past from now. " +
                    "The '{workerJobType}' will now wait.",
                    typeof(BinaryReferencedEvent).Name,
                    dateToStart,
                    typeof(BinariesCleanupJob).Name);
            }
        }

        private static (TimeSpan range, TimeSpan delay) ParseArgs(IReadOnlyDictionary<string, string[]> args)
        {
            if (!args.TryGetValue(CommandLine.Arguments.Range, out var value) || value.Length == 0)
            {
                throw new ArgumentException($"{nameof(CommandLine.Arguments.Range)} argument not specified.");
            }

            if (!TimeSpan.TryParse(value[0], out var range))
            {
                throw new ArgumentException($"{nameof(CommandLine.Arguments.Range)} argument format is incorrect. Make sure that it's specified like 'd:hh:mm:ss'.");
            }

            if (!args.TryGetValue(CommandLine.Arguments.Delay, out value) || value.Length == 0)
            {
                throw new ArgumentException($"{nameof(CommandLine.Arguments.Delay)} argument not specified.");
            }

            if (!TimeSpan.TryParse(value[0], out var delay))
            {
                throw new ArgumentException($"{nameof(CommandLine.Arguments.Delay)} argument format is incorrect. Make sure that it's specified like 'hh:mm:ss'.");
            }

            return (range, delay);
        }

        private async Task<(int totalSessionsCount, int expiredSessionsCount)> RemoveUnreferencedBinaries(
            DateTime periodEnd,
            ICollection<Guid> sessionsWithReferences,
            CancellationToken cancellationToken)
        {
            var totalSessionsCount = 0;
            var expiredSessionsCount = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                var sessionCreatingEvents = _eventReceiver.Receive<SessionCreatingEvent>(_sessionsTopicName, _consumingBatchSize);
                var expiredSessionEvents = sessionCreatingEvents.Where(x => x.Source.ExpiresAt <= periodEnd).ToList();

                totalSessionsCount += sessionCreatingEvents.Count;
                expiredSessionsCount += expiredSessionEvents.Count;
                if (expiredSessionEvents.Count == 0)
                {
                    break;
                }

                foreach (var expiredSessionEvent in expiredSessionEvents)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

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

            return (totalSessionsCount, expiredSessionsCount);
        }
    }
}

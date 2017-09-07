using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Events;
using NuClear.VStore.Kafka;
using NuClear.VStore.Options;
using NuClear.VStore.Sessions;

using Polly;
using Polly.Retry;

namespace NuClear.VStore.Worker.Jobs
{
    public class BinariesCleanupJob : AsyncJob, IDisposable
    {
        private const string GroupId = "vstore-sessions-cleaner";
        private const char SlashChar = '/';

        private static readonly TimeSpan FailDelay = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan SafetyPeriod = TimeSpan.FromMinutes(5);

        private readonly string _binariesReferencesTopicName;

        private readonly ILogger<BinariesCleanupJob> _logger;
        private readonly SessionCleanupService _sessionCleanupService;
        private readonly EventReader _referencesEventReader;
        private readonly EventReceiver _sessionsEventReceiver;

        private CancellationTokenRegistration _cancellationTokenRegistration;

        public BinariesCleanupJob(
            ILogger<BinariesCleanupJob> logger,
            KafkaOptions kafkaOptions,
            SessionCleanupService sessionCleanupService)
        {
            _binariesReferencesTopicName = kafkaOptions.BinariesReferencesTopic;

            _logger = logger;
            _sessionCleanupService = sessionCleanupService;

            _referencesEventReader = new EventReader(logger, kafkaOptions.BrokerEndpoints);
            _sessionsEventReceiver = new EventReceiver(
                logger,
                kafkaOptions.BrokerEndpoints,
                $"{GroupId}-{kafkaOptions.ConsumerGroupToken}",
                new[] { kafkaOptions.SessionEventsTopic });
        }

        public void Dispose()
        {
            _cancellationTokenRegistration.Dispose();
            _referencesEventReader.Dispose();
            _sessionsEventReceiver.Dispose();
        }

        protected override async Task ExecuteInternalAsync(IReadOnlyDictionary<string, string[]> args, CancellationToken cancellationToken)
        {
            var (batchSize, delay) = ParseArgs(args);

            var subscription = CleanupBinaries(batchSize, delay, cancellationToken);

            var taskCompletionSource = new TaskCompletionSource<object>();
            _cancellationTokenRegistration = cancellationToken.Register(
                () =>
                    {
                        subscription.Dispose();
                        taskCompletionSource.SetResult(null);
                    });
            await taskCompletionSource.Task;
        }

        private static (int, TimeSpan) ParseArgs(IReadOnlyDictionary<string, string[]> args)
        {
            if (!args.TryGetValue(CommandLine.Arguments.BatchSize, out var value) || value.Length == 0)
            {
                throw new ArgumentException($"'{nameof(CommandLine.Arguments.BatchSize)}' argument not specified.");
            }

            if (!int.TryParse(value[0], out var batchSize) || batchSize <= 0)
            {
                throw new ArgumentException($"'{nameof(CommandLine.Arguments.BatchSize)}' argument format is incorrect. Make sure that it has been specified as positive integer.");
            }

            if (!args.TryGetValue(CommandLine.Arguments.Delay, out value) || value.Length == 0)
            {
                throw new ArgumentException($"'{nameof(CommandLine.Arguments.Delay)}' argument not specified.");
            }

            if (!TimeSpan.TryParse(value[0], out var delay))
            {
                throw new ArgumentException($"'{nameof(CommandLine.Arguments.Delay)}' argument format is incorrect. Make sure that it has been specified like 'hh:mm:ss'.");
            }

            return (batchSize, delay);
        }

        private static RetryPolicy CreateRetryPolicy(ILogger logger)
        {
            return Policy.Handle<Exception>()
                         .WaitAndRetryForever(
                             attempt => FailDelay,
                             (ex, duration) =>
                                 {
                                     logger.LogError(
                                         new EventId(),
                                         ex,
                                         "Unexpected error occured: {errorMessage}. The {workerJobType} will retry in {delay}.",
                                         ex.Message,
                                         typeof(BinariesCleanupJob).Name,
                                         FailDelay);
                                 });
        }

        private static RetryPolicy<List<KafkaEvent<BinaryReferencedEvent>>> CreateReferenceEventsReadingPolicy(
            ILogger logger,
            DateTime dateToStart,
            TimeSpan delay)
        {
            return Policy<List<KafkaEvent<BinaryReferencedEvent>>>
                .HandleResult(result => result.Count == 0)
                .WaitAndRetryForeverAsync(
                    attempt => delay,
                    (current, duration) => logger.LogWarning(
                        "There are no events of type {eventType} in time interval from {dateToStart:u} till now. " +
                        "The {workerJobType} will now wait for {totalWaitTime:g}.",
                        typeof(BinaryReferencedEvent).Name,
                        dateToStart,
                        typeof(BinariesCleanupJob).Name,
                        duration));
        }

        private static Guid EvaluateSessionId(KafkaEvent<BinaryReferencedEvent> @event)
        {
            var binaryReferencedEvent = @event.Source;

            var fileKey = binaryReferencedEvent.FileKey;
            if (string.IsNullOrEmpty(fileKey))
            {
                throw new ArgumentException(
                    $"File key is not set for the object with id = '{binaryReferencedEvent.ObjectId}' and versionId = {binaryReferencedEvent.ObjectVersionId} " +
                    $"in the event of type {binaryReferencedEvent.GetType().Name} with offset '{@event.TopicPartitionOffset}'");
            }

            return new Guid(fileKey.Substring(0, fileKey.IndexOf(SlashChar)));
        }

        private IDisposable CleanupBinaries(int batchSize, TimeSpan delay, CancellationToken cancellationToken)
        {
            (IReadOnlyCollection<KafkaEvent<SessionCreatingEvent>>, DateTime) EvaluateExpiredSessions(
                IEnumerable<KafkaEvent<SessionCreatingEvent>> sessionCreatingEvents,
                IEnumerable<KafkaEvent<BinaryReferencedEvent>> referenceEvents)
            {
                var lastReferenceEvent = referenceEvents.Last();
                var periodEnd = lastReferenceEvent.Source.ReferencedAt ?? lastReferenceEvent.Timestamp.UtcDateTime;

                _logger.LogInformation("Evaluating the number of expired sessions by '{periodEnd:u}'.", periodEnd);
                return (sessionCreatingEvents.Where(x => x.Source.ExpiresAt <= periodEnd).ToList(), periodEnd);
            }

            async Task ProcessAsync(IList<KafkaEvent<SessionCreatingEvent>> sessionCreatingEvents)
            {
                var oldestSessionDate = sessionCreatingEvents[0].Timestamp.UtcDateTime;
                var dateToStart = oldestSessionDate.Subtract(SafetyPeriod);

                _logger.LogInformation(
                    "Starting to process '{totalSessionsCount}' sessions. Oldest session date: {oldestSessionDate:u}. " +
                    "Binary reference events stream will be read starting from {dateToStart:u}.",
                    sessionCreatingEvents.Count,
                    oldestSessionDate,
                    dateToStart);

                var referenceEventsPolicy = CreateReferenceEventsReadingPolicy(_logger, dateToStart, delay);
                var referenceEvents = await referenceEventsPolicy.ExecuteAsync(
                                          async () =>
                                              {
                                                  var result = await _referencesEventReader.ReadAsync<BinaryReferencedEvent>(
                                                                   _binariesReferencesTopicName,
                                                                   dateToStart);
                                                  return new List<KafkaEvent<BinaryReferencedEvent>>(result);
                                              });

                _logger.LogInformation(
                    "'{referenceEventsCount}' events of type {eventType} read in time interval from {dateToStart:u} till now.",
                    referenceEvents.Count,
                    typeof(BinaryReferencedEvent).Name,
                    dateToStart);

                var (expiredSessionEvents, periodEnd) = EvaluateExpiredSessions(sessionCreatingEvents, referenceEvents);
                while (expiredSessionEvents.Count != sessionCreatingEvents.Count)
                {
                    _logger.LogWarning(
                        "There are only '{expiredSessionsCount}' of '{totalSessionsCount}' sessions expired by {periodEnd:u} in current batch. " +
                        "The {workerJobType} will now wait for {totalWaitTime:g}.",
                        expiredSessionEvents.Count,
                        sessionCreatingEvents.Count,
                        periodEnd,
                        typeof(BinariesCleanupJob).Name,
                        delay);
                    await Task.Delay(delay, cancellationToken);

                    var additional = await _referencesEventReader.ReadAsync<BinaryReferencedEvent>(_binariesReferencesTopicName, periodEnd.Subtract(SafetyPeriod));
                    referenceEvents.AddRange(additional);
                    var (extendedExpired, extendedPeriodEnd) = EvaluateExpiredSessions(sessionCreatingEvents, referenceEvents);

                    expiredSessionEvents = extendedExpired;
                    periodEnd = extendedPeriodEnd;
                }

                var sessionsWithReferences = new HashSet<Guid>(referenceEvents.Select(EvaluateSessionId));
                _logger.LogInformation(
                    "Starting to archive unreferenced sessions in batch of '{expiredSessionsCount}' sessions " +
                    "considering that '{referencedSessionsCount}' sessions were referenced in time interval from {dateToStart:u} till now.",
                    expiredSessionEvents.Count,
                    sessionsWithReferences.Count,
                    dateToStart);

                var archievedSessionsCount = await ArchieveUnreferencedBinaries(expiredSessionEvents, sessionsWithReferences, cancellationToken);
                _logger.LogInformation(
                    "Total '{totalSessionsCount}' sessions has been processed, '{archievedSessionsCount}' were archived.",
                    sessionCreatingEvents.Count,
                    archievedSessionsCount);
            }

            var observable = _sessionsEventReceiver.Subscribe<SessionCreatingEvent>(cancellationToken);
            return observable
                .Buffer(batchSize)
                .Do(batch =>
                        {
                            var retry = CreateRetryPolicy(_logger);
                            retry.Execute(() => ProcessAsync(batch).GetAwaiter().GetResult());
                        })
                .Subscribe();
        }

        private async Task<int> ArchieveUnreferencedBinaries(
            IEnumerable<KafkaEvent<SessionCreatingEvent>> expiredSessionEvents,
            ICollection<Guid> sessionsWithReferences,
            CancellationToken cancellationToken)
        {
            var count = 0;
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

                var sessionExpiresAt = expiredSessionEvent.Source.ExpiresAt;
                var fileCount = await _sessionCleanupService.ArchiveSessionAsync(sessionId, sessionExpiresAt);
                if (fileCount > 0)
                {
                    _logger.LogInformation(
                        "Session '{sessionId}' cleaned up as expired and unused. File count: '{fileCount}'. " +
                        "Session expiration date: {sessionExpiresAt:u}",
                        sessionId,
                        fileCount,
                        sessionExpiresAt);
                }
                else
                {
                    _logger.LogWarning("Session '{sessionId}' not found.", sessionId);
                }

                await _sessionsEventReceiver.CommitAsync(expiredSessionEvent);
                ++count;
            }

            return count;
        }
    }
}

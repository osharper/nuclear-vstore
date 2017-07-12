using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Confluent.Kafka;
using Confluent.Kafka.Serialization;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Events;

namespace NuClear.VStore.Kafka
{
    public sealed class EventReceiver : IDisposable
    {
        private const int DefaultPartition = 0;

        private readonly ILogger _logger;
        private readonly Consumer<string, string> _consumer;

        public EventReceiver(ILogger logger, string brokerEndpoints, string groupId)
        {
            _logger = logger;

            var consumerConfig = new Dictionary<string, object>
                {
                    { "bootstrap.servers", brokerEndpoints },
                    { "group.id", groupId },
                    { "api.version.request", true },
                    { "socket.blocking.max.ms", 5 },
                    { "enable.auto.commit", false },
                    { "fetch.wait.max.ms", 5 },
                    { "fetch.error.backoff.ms", 50 },
                    {
                        "default.topic.config",
                        new Dictionary<string, object>
                            {
                                { "auto.offset.reset", "smallest" }
                            }
                    }
                };
            _consumer = new Consumer<string, string>(consumerConfig, new StringDeserializer(Encoding.UTF8), new StringDeserializer(Encoding.UTF8));
            _consumer.OnLog += OnLog;
            _consumer.OnError += OnLogError;
            _consumer.OnStatistics += OnStatistics;
        }

        public IReadOnlyCollection<KafkaEvent<TSourceEvent>> Receive<TSourceEvent>(string topic, int batchSize) where TSourceEvent : IEvent
            => Receive<TSourceEvent>(c => c.Subscribe(topic), batchSize);

        public IReadOnlyCollection<KafkaEvent<TSourceEvent>> Receive<TSourceEvent>(string topic, DateTime dateToStart)
            where TSourceEvent : IEvent
        {
            var offsets = GetOffsets(topic, dateToStart);
            return Receive<TSourceEvent>(c => c.Assign(offsets));
        }

        public async Task CommitAsync(IEnumerable<TopicPartitionOffset> topicPartitionOffsets)
        {
            var offsetsToCommit = topicPartitionOffsets.Select(x => new TopicPartitionOffset(x.TopicPartition, x.Offset + 1)).ToList();
            if (offsetsToCommit.Count != 0)
            {
                await _consumer.CommitAsync(offsetsToCommit);
            }
        }

        public void Dispose()
        {
            if (_consumer != null)
            {
                _consumer.OnLog -= OnLog;
                _consumer.OnError -= OnLogError;
                _consumer.OnStatistics -= OnStatistics;
                _consumer.Dispose();
            }
        }

        private IReadOnlyCollection<KafkaEvent<TSourceEvent>> Receive<TSourceEvent>(
            Action<Consumer<string, string>> assignOfSubscribe,
            int batchSize = int.MaxValue)
            where TSourceEvent : IEvent
        {
            var isEofReached = false;
            var count = 0;
            var events = new List<KafkaEvent<TSourceEvent>>();

            void OnMessage(object sender, Message<string, string> message)
            {
                var @event = Event.Deserialize<TSourceEvent>(message.Value);
                events.Add(new KafkaEvent<TSourceEvent>(@event, message.TopicPartitionOffset));
                ++count;
            }

            void OnPartitionEof(object sender, TopicPartitionOffset offset) => isEofReached = true;

            void OnConsumeError(object sender, Message error)
            {
                _logger.LogError(
                            "Error consuming from Kafka. Topic/partition/offset: '{kafkaTopic}/{kafkaPartition}/{kafkaOffset}'. Message: '{kafkaError}'.",
                            error.Topic,
                            error.Partition,
                            error.Offset,
                            error.Error);
                throw new EventProcessingException(error.Error.ToString());
            }

            _consumer.OnMessage += OnMessage;
            _consumer.OnPartitionEOF += OnPartitionEof;
            _consumer.OnConsumeError += OnConsumeError;

            try
            {
                assignOfSubscribe(_consumer);
                while (!isEofReached && count < batchSize)
                {
                    _consumer.Poll(TimeSpan.FromMilliseconds(100));
                }
            }
            finally
            {
                _consumer.OnMessage -= OnMessage;
                _consumer.OnPartitionEOF -= OnPartitionEof;
                _consumer.OnConsumeError -= OnConsumeError;
            }

            return events;
        }

        private IEnumerable<TopicPartitionOffset> GetOffsets(string topic, DateTime date)
        {
            var unixTimestamp = Timestamp.DateTimeToUnixTimestampMs(date);
            var timestampToSearch = new TopicPartitionTimestamp(topic, DefaultPartition, new Timestamp(unixTimestamp, TimestampType.CreateTime));
            return _consumer.OffsetsForTimes(new[] { timestampToSearch }, TimeSpan.FromSeconds(10));
        }

        private void OnLog(object sender, LogMessage logMessage)
            => _logger.LogDebug(
                "Consuming from Kafka. Client: '{kafkaClient}', syslog level: '{kafkaLogLevel}', message: '{kafkaLogMessage}'.",
                logMessage.Name,
                logMessage.Level,
                logMessage.Message);

        private void OnLogError(object sender, Error error)
            => _logger.LogInformation("Consuming from Kafka. Client error: '{kafkaError}'. No action required.", error);

        private void OnStatistics(object sender, string json)
            => _logger.LogDebug("Consuming from Kafka. Statistics: '{kafkaStatistics}'.", json);
    }
}
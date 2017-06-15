using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Confluent.Kafka;
using Confluent.Kafka.Serialization;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using NuClear.VStore.Events;
using NuClear.VStore.Json;
using NuClear.VStore.Options;

namespace NuClear.VStore.Kafka
{
    public sealed class EventReader
    {
        private readonly Dictionary<string, object> _consumerConfig = new Dictionary<string, object>();
        private readonly ILogger<EventReader> _logger;

        public EventReader(KafkaOptions kafkaOptions, ILogger<EventReader> logger)
        {
            _logger = logger;
            _consumerConfig.Add("bootstrap.servers", kafkaOptions.BrokerEndpoints);
            _consumerConfig.Add("group.id", kafkaOptions.GroupId);
            _consumerConfig.Add("api.version.request", true);
            _consumerConfig.Add("enable.auto.commit", false);
            _consumerConfig.Add(
                "default.topic.config",
                new Dictionary<string, object>
                    {
                        { "auto.offset.reset", "smallest" }
                    });
        }

        public IReadOnlyCollection<KafkaEvent<TSourceEvent>> Read<TSourceEvent>(string topic, int batchSize) where TSourceEvent : IEvent
        {
            var isEofReached = false;
            var count = 0;
            var events = new List<KafkaEvent<TSourceEvent>>();
            using (var consumer = new Consumer<Null, string>(_consumerConfig, null, new StringDeserializer(Encoding.UTF8)))
            {
                consumer.OnLog += (_, logMessage) => Log(logMessage);
                consumer.OnError += (_, error) => LogError(error);
                consumer.OnStatistics += (_, json) => LogStatistics(json);

                consumer.OnMessage +=
                    (_, message) =>
                        {
                            var @event = JsonConvert.DeserializeObject<TSourceEvent>(message.Value, SerializerSettings.Default);
                            events.Add(new KafkaEvent<TSourceEvent>(@event, message.TopicPartitionOffset));
                            ++count;
                        };

                consumer.OnPartitionEOF += (_, offset) => isEofReached = true;

                consumer.OnConsumeError +=
                    (_, error) =>
                        {
                            _logger.LogError(
                                "Error consuming from Kafka. Topic/partition/offset: '{kafkaTopic}/{kafkaPartition}/{kafkaOffset}'. Message: '{kafkaError}'.",
                                error.Topic,
                                error.Partition,
                                error.Offset,
                                error.Error);
                            throw new EventProcessingException(error.Error.ToString());
                        };

                consumer.Subscribe(topic);
                while (!isEofReached && count < batchSize)
                {
                    consumer.Poll(5);
                }

                return events;
            }
        }

        public IReadOnlyCollection<TSourceEvent> ReadPartition<TSourceEvent>(string topic, int partition) where TSourceEvent : IEvent
        {
            var isEofReached = false;
            var events = new List<TSourceEvent>();
            using (var consumer = new Consumer<Null, string>(_consumerConfig, null, new StringDeserializer(Encoding.UTF8)))
            {
                consumer.OnLog += (_, logMessage) => Log(logMessage);
                consumer.OnError += (_, error) => LogError(error);
                consumer.OnStatistics += (_, json) => LogStatistics(json);

                consumer.OnMessage +=
                    (_, message) =>
                        {
                            var @event = JsonConvert.DeserializeObject<TSourceEvent>(message.Value, SerializerSettings.Default);
                            events.Add(@event);
                        };

                consumer.OnPartitionEOF += (_, offset) => isEofReached = true;

                consumer.OnConsumeError +=
                    (_, error) =>
                        {
                            _logger.LogError(
                                "Error consuming from Kafka. Topic/partition/offset: '{kafkaTopic}/{kafkaPartition}/{kafkaOffset}'. Message: '{kafkaError}'.",
                                error.Topic,
                                error.Partition,
                                error.Offset,
                                error.Error);
                            throw new EventProcessingException(error.Error.ToString());
                        };

                consumer.Assign(new[] { new TopicPartitionOffset(topic, partition, 0) });
                while (!isEofReached)
                {
                    consumer.Poll(5);
                }

                return events;
            }
        }

        public async Task CommitAsync(IEnumerable<TopicPartitionOffset> topicPartitionOffsets)
        {
            using (var consumer = new Consumer<Null, string>(_consumerConfig, null, new StringDeserializer(Encoding.UTF8)))
            {
                consumer.OnLog += (_, logMessage) => Log(logMessage);
                consumer.OnError += (_, error) => LogError(error);
                consumer.OnStatistics += (_, json) => LogStatistics(json);

                await consumer.CommitAsync(topicPartitionOffsets);
            }
        }

        private void Log(LogMessage logMessage)
            => _logger.LogInformation(
                "Consuming from Kafka. Client: '{kafkaClient}', syslog level: '{kafkaLogLevel}', message: '{kafkaLogMessage}'.",
                logMessage.Name,
                logMessage.Level,
                logMessage.Message);

        private void LogError(Error error)
            => _logger.LogInformation("Consuming from Kafka. Client error: '{kafkaError}'. No action required.", error);

        private void LogStatistics(string json)
            => _logger.LogDebug("Consuming from Kafka. Statistics: '{kafkaStatistics}'.", json);
    }
}
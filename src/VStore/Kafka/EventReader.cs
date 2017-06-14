using System;
using System.Collections.Generic;
using System.Linq;
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
            _consumerConfig.Add("session.timeout.ms", 600);
            _consumerConfig.Add(
                "default.topic.config",
                new Dictionary<string, object>
                    {
                        { "auto.offset.reset", "smallest" }
                    });
        }

        public IReadOnlyCollection<TopicPartitionOffset> EvaluateAssignments(string topic, DateTime earliestDateTime, long rewindCount)
        {
            IReadOnlyCollection<TopicPartitionOffset> assigntments = null;
            using (var consumer = new Consumer<Null, string>(_consumerConfig, null, new StringDeserializer(Encoding.UTF8)))
            {
                consumer.Subscribe(topic);
                consumer.OnLog += (_, logMessage) => Log(logMessage);
                consumer.OnError += (_, error) => LogError(error);
                consumer.OnStatistics += (_, json) => LogStatistics(json);

                consumer.OnMessage +=
                    (sender, message) =>
                        {
                            var consumerInstance = (Consumer<Null, string>)sender;
                            if (message.Offset != 0 && message.Timestamp.UtcDateTime > earliestDateTime)
                            {
                                var assignments =
                                    consumerInstance.Assignment.Select(x => new TopicPartitionOffset(x, message.Offset - rewindCount));
                                consumerInstance.Assign(assignments);
                            }
                            else
                            {
                                assigntments = consumerInstance.Assignment.Select(x => new TopicPartitionOffset(x, message.Offset)).ToList();
                            }
                        };

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

                while (assigntments == null)
                {
                    consumer.Poll(TimeSpan.FromMilliseconds(100));
                }

                return assigntments;
            }
        }

        public IReadOnlyCollection<KafkaEvent> Read(string topic, int batchSize)
        {
            var count = 0;
            var events = new List<KafkaEvent>();
            using (var consumer = new Consumer<Null, string>(_consumerConfig, null, new StringDeserializer(Encoding.UTF8)))
            {
                consumer.Subscribe(topic);

                consumer.OnLog += (_, logMessage) => Log(logMessage);
                consumer.OnError += (_, error) => LogError(error);
                consumer.OnStatistics += (_, json) => LogStatistics(json);

                consumer.OnMessage +=
                    (_, message) =>
                        {
                            var @event = JsonConvert.DeserializeObject<IEvent>(message.Value, SerializerSettings.Default);
                            events.Add(new KafkaEvent(@event, message.TopicPartitionOffset));
                            ++count;
                        };

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

                while (count < batchSize)
                {
                    consumer.Poll(TimeSpan.FromMilliseconds(100));
                }

                return events;
            }
        }

        public IReadOnlyCollection<KafkaEvent> Read(IReadOnlyCollection<TopicPartitionOffset> assignments, int batchSize)
        {
            var count = 0;
            var events = new List<KafkaEvent>();
            using (var consumer = new Consumer<Null, string>(_consumerConfig, null, new StringDeserializer(Encoding.UTF8)))
            {
                consumer.Assign(assignments);

                consumer.OnLog += (_, logMessage) => Log(logMessage);
                consumer.OnError += (_, error) => LogError(error);
                consumer.OnStatistics += (_, json) => LogStatistics(json);

                consumer.OnMessage +=
                    (sender, message) =>
                        {
                            var @event = JsonConvert.DeserializeObject<IEvent>(message.Value, SerializerSettings.Default);
                            events.Add(new KafkaEvent(@event, message.TopicPartitionOffset));
                            ++count;
                        };

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

                while (count < batchSize)
                {
                    consumer.Poll(TimeSpan.FromMilliseconds(100));
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
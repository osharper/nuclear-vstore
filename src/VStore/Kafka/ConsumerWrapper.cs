using System;
using System.Collections.Generic;
using System.Text;

using Confluent.Kafka;
using Confluent.Kafka.Serialization;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Options;

namespace NuClear.VStore.Kafka
{
    public abstract class ConsumerWrapper : IDisposable
    {
        private readonly ILogger _logger;

        protected ConsumerWrapper(ILogger logger, KafkaOptions kafkaOptions, string groupId = null)
        {
            _logger = logger;
            Consumer = new Consumer<string, string>(
                CreateConsumerConfig(kafkaOptions, groupId),
                new StringDeserializer(Encoding.UTF8),
                new StringDeserializer(Encoding.UTF8));

            Consumer.OnLog += OnLog;
            Consumer.OnError += OnError;
            Consumer.OnStatistics += OnStatistics;
            Consumer.OnConsumeError += OnConsumeError;
        }

        protected Consumer<string, string> Consumer { get; }

        public virtual void Dispose()
        {
            Consumer.OnLog -= OnLog;
            Consumer.OnError -= OnError;
            Consumer.OnStatistics -= OnStatistics;
            Consumer.OnConsumeError -= OnConsumeError;
            Consumer.Dispose();
        }

        private static Dictionary<string, object> CreateConsumerConfig(KafkaOptions kafkaOptions, string groupId)
            => new Dictionary<string, object>
                {
                    // https://github.com/edenhill/librdkafka/blob/master/CONFIGURATION.md
                    { "bootstrap.servers", kafkaOptions.BrokerEndpoints },
                    { "api.version.request", true },
                    { "group.id", !string.IsNullOrEmpty(groupId) ? groupId : Guid.NewGuid().ToString() },
                    { "enable.auto.commit", kafkaOptions.Consumer.EnableAutoCommit },
                    { "fetch.wait.max.ms", kafkaOptions.Consumer.FetchWaitMaxMs },
                    { "fetch.error.backoff.ms", kafkaOptions.Consumer.FetchErrorBackoffMs },
                    { "fetch.message.max.bytes", kafkaOptions.Consumer.FetchMessageMaxBytes },
                    { "queued.min.messages", kafkaOptions.Consumer.QueuedMinMessages },
#if DEBUG
                    { "debug", "msg" },
                    { "socket.blocking.max.ms", 1 }, // https://github.com/edenhill/librdkafka/wiki/How-to-decrease-message-latency
#else
                    { "log.connection.close", false },
#endif
                {
                        "default.topic.config",
                        new Dictionary<string, object>
                            {
                                { "auto.offset.reset", "beginning" }
                            }
                    }
                };

        private void OnLog(object sender, LogMessage logMessage)
            => _logger.LogDebug(
                "Consuming from Kafka. Client: '{kafkaClient}', syslog level: '{kafkaLogLevel}', message: '{kafkaLogMessage}'.",
                logMessage.Name,
                logMessage.Level,
                logMessage.Message);

        private void OnError(object sender, Error error)
            => _logger.LogInformation("Consuming from Kafka. Client error: '{kafkaError}'. No action required.", error);

        private void OnStatistics(object sender, string json)
            => _logger.LogDebug("Consuming from Kafka. Statistics: '{kafkaStatistics}'.", json);

        private void OnConsumeError(object sender, Message error)
        {
            _logger.LogError(
                "Error consuming from Kafka. Topic/partition/offset: '{kafkaTopic}/{kafkaPartition}/{kafkaOffset}'. Message: '{kafkaError}'.",
                error.Topic,
                error.Partition,
                error.Offset,
                error.Error);
            throw new EventProcessingException(error.Error.ToString());
        }
    }
}
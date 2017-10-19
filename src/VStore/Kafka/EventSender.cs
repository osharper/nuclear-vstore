using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Confluent.Kafka;
using Confluent.Kafka.Serialization;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Events;
using NuClear.VStore.Options;

namespace NuClear.VStore.Kafka
{
    public sealed class EventSender : IDisposable
    {
        private const int DefaultPartition = 0;

        private readonly ILogger<EventSender> _logger;
        private readonly Producer<string, string> _producer;

        public EventSender(KafkaOptions kafkaOptions, ILogger<EventSender> logger)
        {
            _logger = logger;

            var producerConfig = new Dictionary<string, object>
                {
                    // https://github.com/edenhill/librdkafka/blob/master/CONFIGURATION.md
                    { "bootstrap.servers", kafkaOptions.BrokerEndpoints },
                    { "api.version.request", true },
                    { "queue.buffering.max.ms", kafkaOptions.Producer.QueueBufferingMaxMs },
                    { "queue.buffering.max.kbytes", kafkaOptions.Producer.QueueBufferingMaxKbytes },
                    { "batch.num.messages", kafkaOptions.Producer.BatchNumMessages },
                    { "message.max.bytes", kafkaOptions.Producer.MessageMaxBytes },
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
                                { "message.timeout.ms", 5000 },
                                { "request.required.acks", -1 }
                            }
                    }
                };
            _producer = new Producer<string, string>(producerConfig, new StringSerializer(Encoding.UTF8), new StringSerializer(Encoding.UTF8));
            _producer.OnLog += OnLog;
            _producer.OnError += OnLogError;
            _producer.OnStatistics += OnStatistics;
        }

        public async Task SendAsync(string topic, IEvent @event)
        {
            var message = @event.Serialize();
            try
            {
                var result = await _producer.ProduceAsync(topic, @event.Key, message, DefaultPartition);
                _logger.LogDebug(
                    "Produced to Kafka. Topic/partition/offset: '{kafkaTopic}/{kafkaPartition}/{kafkaOffset}'. Message: '{kafkaMessage}'.",
                    result.Topic,
                    result.Partition,
                    result.Offset,
                    message);
                if (result.Error.HasError)
                {
                    throw new KafkaException(result.Error);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    new EventId(),
                    ex,
                    "Error producing to Kafka. Topic: '{kafkaTopic}'. Message: {kafkaMessage}'.",
                    topic,
                    message);
                throw;
            }
        }

        public void Dispose()
        {
            if (_producer != null)
            {
                _producer.OnLog -= OnLog;
                _producer.OnError -= OnLogError;
                _producer.OnStatistics -= OnStatistics;
                _producer.Dispose();
            }
        }

        private void OnLog(object sender, LogMessage logMessage)
            => _logger.LogDebug(
                "Producing to Kafka. Client: '{kafkaClient}', syslog level: '{kafkaLogLevel}', message: '{kafkaLogMessage}'.",
                logMessage.Name,
                logMessage.Level,
                logMessage.Message);

        private void OnLogError(object sender, Error error)
            => _logger.LogInformation("Producing to Kafka. Client error: '{kafkaError}'. No action required.", error);

        private void OnStatistics(object sender, string json)
            => _logger.LogDebug("Producing to Kafka. Statistics: '{kafkaStatistics}'.", json);
    }
}
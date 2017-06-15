using System;
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
    public sealed class EventSender : IDisposable
    {
        private readonly ILogger<EventSender> _logger;
        private readonly Producer<Null, string> _producer;

        public EventSender(KafkaOptions kafkaOptions, ILogger<EventSender> logger)
        {
            _logger = logger;

            var producerConfig = new Dictionary<string, object>
                                     {
                                         { "bootstrap.servers", kafkaOptions.BrokerEndpoints },
                                         { "api.version.request", true },
                                         { "queue.buffering.max.ms", 5 }
                                     };
            _producer = new Producer<Null, string>(producerConfig, null, new StringSerializer(Encoding.UTF8));
            _producer.OnLog += (_, logMessage) => Log(logMessage);
            _producer.OnError += (_, error) => LogError(error);
            _producer.OnStatistics += (_, json) => LogStatistics(json);
        }

        public async Task SendAsync(string topic, IEvent @event)
        {
            await SendAsync(topic, @event, message => _producer.ProduceAsync(topic, null, message));
        }

        public async Task SendAsync(string topic, int partition, IEvent @event)
        {
            await SendAsync(topic, @event, message => _producer.ProduceAsync(topic, null, message, partition));
        }

        public void Dispose()
        {
            _producer?.Dispose();
        }

        private async Task SendAsync(string topic, IEvent @event, Func<string, Task<Message<Null, string>>> producer)
        {
            var message = JsonConvert.SerializeObject(@event, SerializerSettings.Default);

            try
            {
                var result = await producer(message);

                // we must do this call as workaround https://github.com/confluentinc/confluent-kafka-dotnet/issues/190
                await Task.Yield();

                _logger.LogInformation(
                    "Produced to Kafka. Topic/partition/offset: '{kafkaTopic}/{kafkaPartition}/{kafkaOffset}'. Message: '{kafkaMessage}'.",
                    result.Topic,
                    result.Partition,
                    result.Offset,
                    message);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    new EventId(),
                    ex,
                    "Error producing to Kafka. Topic: '{kafkaTopic}'. Message: {kafkaMessage}. Error: '{kafkaError}'.",
                    message,
                    topic);
                throw;
            }
        }

        private void Log(LogMessage logMessage)
            => _logger.LogInformation(
                "Producing to Kafka. Client: '{kafkaClient}', syslog level: '{kafkaLogLevel}', message: '{kafkaLogMessage}'.",
                logMessage.Name,
                logMessage.Level,
                logMessage.Message);

        private void LogError(Error error)
            => _logger.LogInformation("Producing to Kafka. Client error: '{kafkaError}'. No action required.", error);

        private void LogStatistics(string json)
            => _logger.LogDebug("Producing to Kafka. Statistics: '{kafkaStatistics}'.", json);
    }
}
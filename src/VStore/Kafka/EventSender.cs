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
    public sealed class EventSender
    {
        private readonly Dictionary<string, object> _producerConfig = new Dictionary<string, object>();
        private readonly ILogger<EventSender> _logger;

        public EventSender(KafkaOptions kafkaOptions, ILogger<EventSender> logger)
        {
            _logger = logger;
            _producerConfig.Add("bootstrap.servers", kafkaOptions.BrokerEndpoints);
        }

        public async Task SendAsync(string topic, IEvent @event)
        {
            var message = JsonConvert.SerializeObject(@event, SerializerSettings.Default);
            try
            {
                using (var producer = new Producer<Null, string>(_producerConfig, null, new StringSerializer(Encoding.UTF8)))
                {
                    producer.OnLog += (_, logMessage) => Log(logMessage);
                    producer.OnError += (_, error) => LogError(error);
                    producer.OnStatistics += (_, json) => LogStatistics(json);

                    var result = await producer.ProduceAsync(topic, null, message);
                    _logger.LogInformation(
                        "Producing to Kafka. Topic/partition/offset: '{topic}/{partition}/{offset}'. Message: '{message}'",
                        topic,
                        result.Partition,
                        result.Offset,
                        message);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    new EventId(),
                    ex,
                    "Error producing to Kafka. Topic: '{topic}'. Message: {message}. Error: '{error}'",
                    message,
                    topic);
                throw;
            }
        }

        private void Log(LogMessage logMessage)
            => _logger.LogInformation(
                "Producing to Kafka. Client: '{kafkaClient}', level: '{logLevel}', message: '{logMessage}'",
                logMessage.Name,
                logMessage.Level,
                logMessage.Message);

        private void LogError(Error error)
            => _logger.LogInformation("Producing to Kafka. Client error: '{error}'. No action required.", error);

        private void LogStatistics(string json)
            => _logger.LogDebug($"Producing to Kafka. Statistics: '{json}'");
    }
}
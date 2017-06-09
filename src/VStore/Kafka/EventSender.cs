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
                    var result = await producer.ProduceAsync(topic, null, message);
                    _logger.LogInformation(
                        "Event with content {eventContent} has been sent to the topic '{topic}' on partition '{partition}' with offset '{offset}'",
                        message,
                        topic,
                        result.Partition,
                        result.Topic);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(), ex, "Unexpected error occured while sending a '{eventContent}' to '{topic}'.", message, topic);
                throw;
            }
        }
    }
}
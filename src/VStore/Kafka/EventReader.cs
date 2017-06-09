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
    public sealed class EventReader
    {
        private const int BatchSize = 1000;

        private readonly Dictionary<string, object> _consumerConfig = new Dictionary<string, object>();
        private readonly ILogger<EventReader> _logger;

        public EventReader(KafkaOptions kafkaOptions, ILogger<EventReader> logger)
        {
            _logger = logger;
            _consumerConfig.Add("bootstrap.servers", kafkaOptions.BrokerEndpoints);
            _consumerConfig.Add("group.id", kafkaOptions.GroupId);
            _consumerConfig.Add("api.version.request", "true");
            _consumerConfig.Add("enable.auto.commit", "false");
            _consumerConfig.Add("session.timeout.ms", 600);
            _consumerConfig.Add(
                "efault.topic.config",
                new Dictionary<string, object>
                    {
                        { "auto.offset.reset", "smallest" }
                    });
        }

        public async Task<IReadOnlyCollection<IEvent>> ReadAsync(string topic)
        {
            try
            {
                var events = new List<IEvent>();
                using (var consumer = new Consumer<Null,string>(_consumerConfig, null, new StringDeserializer(Encoding.UTF8)))
                {
                    consumer.Subscribe(topic);

                    var count = 0;
                    consumer.OnMessage +=
                        (_, message) =>
                            {
                                if (count++ < BatchSize)
                                {
                                    var @event = JsonConvert.DeserializeObject<IEvent>(message.Value, SerializerSettings.Default);
                                    events.Add(@event);
                                }
                            };

                    return events;
                }
            }
            catch (Exception ex)
            {
                throw;
            }
        }
    }
}
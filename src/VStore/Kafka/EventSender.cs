using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Confluent.Kafka;
using Confluent.Kafka.Serialization;

using NuClear.VStore.Options;

namespace NuClear.VStore.Kafka
{
    public sealed class EventSender
    {
        private readonly Dictionary<string, object> _producerConfig = new Dictionary<string, object>();
        public EventSender(KafkaOptions kafkaOptions)
        {
            _producerConfig.Add("bootstrap.servers", kafkaOptions.BrokerEndpoints);
        }

        public async Task Send(string topic, string message)
        {
            using (var producer = new Producer<Null, string>(_producerConfig, null, new StringSerializer(Encoding.UTF8)))
            {
                await producer.ProduceAsync(topic, null, message);
            }
        }
    }
}
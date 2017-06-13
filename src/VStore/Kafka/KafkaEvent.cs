using Confluent.Kafka;

using NuClear.VStore.Events;

namespace NuClear.VStore.Kafka
{
    public class KafkaEvent
    {
        public KafkaEvent(IEvent source, string topic)
            : this (source, topic, 0, 0)
        {
        }

        public KafkaEvent(IEvent source, string topic, int partition)
            : this(source, topic, partition, 0)
        {
        }

        public KafkaEvent(IEvent source, string topic, int partition, Offset offset)
            : this (source, new TopicPartitionOffset(topic, partition, offset))
        {
        }

        public KafkaEvent(IEvent source, TopicPartitionOffset topicPartitionOffset)
        {
            Source = source;
            TopicPartitionOffset = topicPartitionOffset;
        }

        public IEvent Source { get; }

        public TopicPartitionOffset TopicPartitionOffset { get; }
    }
}
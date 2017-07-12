using Confluent.Kafka;

using NuClear.VStore.Events;

namespace NuClear.VStore.Kafka
{
    public class KafkaEvent<TSourceEvent> where TSourceEvent : IEvent
    {
        public KafkaEvent(TSourceEvent source, string topic)
            : this (source, topic, 0, 0)
        {
        }

        public KafkaEvent(TSourceEvent source, string topic, int partition)
            : this(source, topic, partition, 0)
        {
        }

        public KafkaEvent(TSourceEvent source, string topic, int partition, Offset offset)
            : this (source, new TopicPartitionOffset(topic, partition, offset))
        {
        }

        public KafkaEvent(TSourceEvent source, TopicPartitionOffset topicPartitionOffset)
        {
            Source = source;
            TopicPartitionOffset = topicPartitionOffset;
        }

        public TSourceEvent Source { get; }

        public TopicPartitionOffset TopicPartitionOffset { get; }
    }
}
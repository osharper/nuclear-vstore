using Confluent.Kafka;

using NuClear.VStore.Events;

namespace NuClear.VStore.Kafka
{
    public class KafkaEvent<TSourceEvent> where TSourceEvent : IEvent
    {
        public KafkaEvent(TSourceEvent source, string topic, int partition, Offset offset, Timestamp timestamp)
            : this (source, new TopicPartitionOffset(topic, partition, offset), timestamp)
        {
        }

        public KafkaEvent(TSourceEvent source, TopicPartitionOffset topicPartitionOffset, Timestamp timestamp)
        {
            Source = source;
            TopicPartitionOffset = topicPartitionOffset;
            Timestamp = timestamp;
        }

        public TSourceEvent Source { get; }
        public TopicPartitionOffset TopicPartitionOffset { get; }
        public Timestamp Timestamp { get; }
    }
}
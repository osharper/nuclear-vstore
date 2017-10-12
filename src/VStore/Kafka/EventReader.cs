using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Confluent.Kafka;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Events;
using NuClear.VStore.Options;

namespace NuClear.VStore.Kafka
{
    public sealed class EventReader : ConsumerWrapper
    {
        private const int DefaultPartition = 0;

        public EventReader(ILogger logger, KafkaOptions kafkaOptions)
            : base(logger, kafkaOptions)
        {
        }

        public async Task<IReadOnlyCollection<KafkaEvent<TSourceEvent>>> ReadAsync<TSourceEvent>(
            string topic, DateTime dateToStart)
            where TSourceEvent : IEvent
        {
            var events = new List<KafkaEvent<TSourceEvent>>();
            var done = false;

            void OnMessage(object sender, Message<string, string> message)
            {
                var @event = Event.Deserialize<TSourceEvent>(message.Value);
                events.Add(new KafkaEvent<TSourceEvent>(@event, message.TopicPartitionOffset, message.Timestamp));
            }

            void OnPartitionEof(object sender, TopicPartitionOffset offset) => done = true;

            Consumer.OnPartitionEOF += OnPartitionEof;
            Consumer.OnMessage += OnMessage;

            try
            {
                var offsets = GetOffsets(topic, dateToStart);
                Consumer.Assign(offsets);
                await Task.Run(() =>
                                   {
                                       while (!done)
                                       {
                                           Consumer.Poll(TimeSpan.FromMilliseconds(100));
                                       }
                                   })
                          .ConfigureAwait(false);
            }
            finally
            {
                Consumer.OnMessage -= OnMessage;
                Consumer.OnPartitionEOF -= OnPartitionEof;
            }

            return events;
        }

        private IEnumerable<TopicPartitionOffset> GetOffsets(string topic, DateTime date)
        {
            try
            {
                var timestampToSearch = new TopicPartitionTimestamp(topic, DefaultPartition, new Timestamp(date, TimestampType.CreateTime));
                return Consumer.OffsetsForTimes(new[] { timestampToSearch }, TimeSpan.FromSeconds(10)).Select(x => (TopicPartitionOffset)x);
            }
            catch (KafkaException ex)
            {
                throw new EventReceivingException(
                    $"Unexpected error occured while getting offset for date '{date:o}' " +
                    $"in topic/partition '{topic}/{DefaultPartition}'",
                    ex);
            }
        }
    }
}
using System;
using System.Collections.Generic;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

using Confluent.Kafka;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Events;

namespace NuClear.VStore.Kafka
{
    public sealed class EventReceiver : ConsumerWrapper
    {
        private readonly IEnumerable<string> _topics;

        private bool _streamingStarted;

        public EventReceiver(ILogger logger, string brokerEndpoints, string groupId, IEnumerable<string> topics)
            : base(logger, brokerEndpoints, groupId)
        {
            _topics = topics;
        }

        public IObservable<KafkaEvent<TSourceEvent>> Subscribe<TSourceEvent>(CancellationToken cancellationToken) where TSourceEvent : IEvent
        {
            if (_streamingStarted)
            {
                throw new InvalidOperationException("Streaming already started. Please dispose the previous obvservable before getting the new one.");
            }

            var pollCancellationTokenSource = new CancellationTokenSource();
            var registration = cancellationToken.Register(() => pollCancellationTokenSource.Cancel());

            var onMessage = Observable.FromEventPattern<Message<string, string>>(
                                          x =>
                                              {
                                                  Consumer.OnMessage += x;
                                                  Consumer.Subscribe(_topics);
                                              },
                                          x =>
                                              {
                                                  pollCancellationTokenSource.Cancel();
                                                  registration.Dispose();
                                                  Consumer.Unsubscribe();
                                                  Consumer.OnMessage -= x;
                                                  _streamingStarted = false;
                                              })
                                      .Select(x => x.EventArgs)
                                      .Select(x =>
                                                  {
                                                      var @event = Event.Deserialize<TSourceEvent>(x.Value);
                                                      return new KafkaEvent<TSourceEvent>(@event, x.TopicPartitionOffset, x.Timestamp);
                                                  });
            Task.Factory.StartNew(
                    () =>
                        {
                            while (!pollCancellationTokenSource.IsCancellationRequested)
                            {
                                Consumer.Poll(TimeSpan.FromMilliseconds(100));
                            }
                        },
                    pollCancellationTokenSource.Token,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default)
                .ConfigureAwait(false);

            _streamingStarted = true;

            return onMessage;
        }

        public async Task CommitAsync<TSourceEvent>(KafkaEvent<TSourceEvent> @event) where TSourceEvent : IEvent
        {
            var offsetsToCommit = new TopicPartitionOffset(@event.TopicPartitionOffset.TopicPartition, @event.TopicPartitionOffset.Offset + 1);
            await Consumer.CommitAsync(new[] { offsetsToCommit });
        }
    }
}
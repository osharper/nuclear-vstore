namespace NuClear.VStore.Options
{
    public sealed class KafkaOptions
    {
        public string BrokerEndpoints { get; set; }
        public string ConsumerGroupToken { get; set; }
        public string ObjectEventsTopic { get; set; }
        public string ObjectVersionsTopic { get; set; }
        public string SessionEventsTopic { get; set; }
        public string BinariesReferencesTopic { get; set; }
        public ConsumerOptions Consumer { get; set; }
        public ProducerOptions Producer { get; set; }

        public sealed class ConsumerOptions
        {
            public bool EnableAutoCommit { get; set; }
            public int FetchWaitMaxMs { get; set; }
            public int FetchErrorBackoffMs { get; set; }
            public int FetchMessageMaxBytes { get; set; }
            public int QueuedMinMessages { get; set; }
        }

        public sealed class ProducerOptions
        {
            public int QueueBufferingMaxMs { get; set; }
            public int QueueBufferingMaxKbytes { get; set; }
            public int BatchNumMessages { get; set; }
            public int MessageMaxBytes { get; set; }
        }
    }
}
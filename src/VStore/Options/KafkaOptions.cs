namespace NuClear.VStore.Options
{
    public sealed class KafkaOptions
    {
        public string BrokerEndpoints { get; set; }
        public int ConsumingBatchSize { get; set; }
        public string ObjectEventsTopic { get; set; }
        public string ObjectVersionsTopic { get; set; }
        public string SessionsTopic { get; set; }
        public string BinariesReferencesTopic { get; set; }
    }
}
namespace NuClear.VStore.Options
{
    public sealed class KafkaOptions
    {
        public string BrokerEndpoints { get; set; }
        public string GroupId { get; set; }
        public string SessionsTopic { get; set; }
        public string UsingsTopic { get; set; }
    }
}
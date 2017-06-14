namespace NuClear.VStore.Events
{
    public sealed class BinaryUsedEvent : IEvent
    {
        public long ObjectId { get; set; }
        public int ElementTemplateCode { get; set; }
        public string FileKey { get; set; }
    }
}
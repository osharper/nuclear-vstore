namespace NuClear.VStore.Events
{
    public sealed class ObjectVersionCreatedEvent : IEvent
    {
        public ObjectVersionCreatedEvent(long objectId, string versionId)
        {
            ObjectId = objectId;
            VersionId = versionId;
        }

        public string Key => ObjectId.ToString();
        public long ObjectId { get; }
        public string VersionId { get; }
    }
}
namespace NuClear.VStore.Events
{
    public sealed class ObjectVersionCreatingEvent : IEvent
    {
        public ObjectVersionCreatingEvent(long objectId, string currentVersionId)
        {
            ObjectId = objectId;
            CurrentVersionId = currentVersionId;
        }

        public string Key => ObjectId.ToString();
        public long ObjectId { get; }
        public string CurrentVersionId { get; }
    }
}
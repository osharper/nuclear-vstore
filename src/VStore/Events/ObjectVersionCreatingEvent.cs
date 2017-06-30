namespace NuClear.VStore.Events
{
    public sealed class ObjectVersionCreatingEvent
    {
        public ObjectVersionCreatingEvent(long objectId, string currentVersionId)
        {
            ObjectId = objectId;
            CurrentVersionId = currentVersionId;
        }

        public long ObjectId { get; }
        public string CurrentVersionId { get; }
    }
}
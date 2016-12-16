using System;

namespace NuClear.VStore.Objects
{
    public class ObjectInconsistentException : Exception
    {
        public ObjectInconsistentException(long objectId, string details)
            : base($"Object '{objectId}' is inconsistent. Details: {details}")
        {
            ObjectId = objectId;
        }

        public long ObjectId { get; }
    }
}
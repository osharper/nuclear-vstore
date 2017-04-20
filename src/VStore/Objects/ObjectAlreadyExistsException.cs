using System;

namespace NuClear.VStore.Objects
{
    public class ObjectAlreadyExistsException : Exception
    {
        public ObjectAlreadyExistsException(long objectId)
        {
            ObjectId = objectId;
        }

        public long ObjectId { get; }
    }
}

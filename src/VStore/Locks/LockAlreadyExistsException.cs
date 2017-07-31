using System;

namespace NuClear.VStore.Locks
{
    public sealed class LockAlreadyExistsException : Exception
    {
        public LockAlreadyExistsException(long rootObjectId)
            : base($"Lock already exists for the object with key {rootObjectId.ToString()}")
        {
        }
    }
}
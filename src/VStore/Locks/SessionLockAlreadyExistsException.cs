using System;

namespace NuClear.VStore.Locks
{
    public sealed class SessionLockAlreadyExistsException : Exception
    {
        public SessionLockAlreadyExistsException(string objectKey)
            : base($"Session lock already exists for the object with key {objectKey}")
        {
        }
    }
}
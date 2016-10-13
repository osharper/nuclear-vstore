using System;

namespace NuClear.VStore.Content
{
    public class ObjectInconsistentException : Exception
    {
        public ObjectInconsistentException(long objectId, string details)
            : base($"Object '{objectId}' is inconsistent. Details: {details}")
        {
        }
    }
}
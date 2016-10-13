using System;

namespace NuClear.VStore.S3
{
    public sealed class ObjectNotFoundException : Exception
    {
        public ObjectNotFoundException(string message) : base (message)
        {
        }
    }
}
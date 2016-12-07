using System;

namespace NuClear.VStore.Objects.Validate
{
    public sealed class IncorrectLinkException : Exception
    {
        public IncorrectLinkException(long objectId)
            : base($"Link '{objectId}' is incorrect")
        {
        }
    }
}

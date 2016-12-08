using System;

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public sealed class IncorrectLinkException : Exception
    {
        public IncorrectLinkException() : base("Link is incorrect")
        {
        }
    }
}

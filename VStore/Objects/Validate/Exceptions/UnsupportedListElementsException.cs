using System;

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class UnsupportedListElementsException : Exception
    {
        public UnsupportedListElementsException() : base("Unsupported list element found")
        {
        }
    }
}

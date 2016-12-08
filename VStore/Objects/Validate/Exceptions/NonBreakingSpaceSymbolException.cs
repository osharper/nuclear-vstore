using System;

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class NonBreakingSpaceSymbolException : Exception
    {
        public NonBreakingSpaceSymbolException() : base("Non-breaking space found")
        {
        }
    }
}

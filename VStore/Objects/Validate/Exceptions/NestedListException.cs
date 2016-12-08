using System;

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class NestedListException : Exception
    {
        public NestedListException() : base("Nested list found")
        {
        }
    }
}

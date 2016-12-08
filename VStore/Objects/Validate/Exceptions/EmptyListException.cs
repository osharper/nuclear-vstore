using System;

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class EmptyListException : Exception
    {
        public EmptyListException() : base("Empty list found")
        {
        }
    }
}

using System;

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public abstract class ObjectElementValidationException : Exception
    {
        protected ObjectElementValidationException(string message) : base(message)
        {
        }

        public abstract ElementValidationErrors ErrorType { get; }
    }
}

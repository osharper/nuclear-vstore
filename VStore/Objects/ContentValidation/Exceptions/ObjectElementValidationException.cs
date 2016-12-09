using System;

namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public abstract class ObjectElementValidationException : Exception
    {
        public abstract ElementValidationErrors ErrorType { get; }
    }
}

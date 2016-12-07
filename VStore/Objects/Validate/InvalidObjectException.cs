using System;
using System.Collections.Generic;

namespace NuClear.VStore.Objects.Validate
{
    public sealed class InvalidObjectElementException : AggregateException
    {
        public InvalidObjectElementException(long objectId, long elementId, IEnumerable<Exception> exceptions) :
            base($"Invalid object {objectId} element {elementId}", exceptions)
        {
        }
    }
}

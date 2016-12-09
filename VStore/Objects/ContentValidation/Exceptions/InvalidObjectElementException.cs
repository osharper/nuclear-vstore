using System;
using System.Collections.Generic;

namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public sealed class InvalidObjectElementException : AggregateException
    {
        public InvalidObjectElementException(long objectId, long elementId, IEnumerable<Exception> exceptions) :
            base($"Invalid object {objectId} element {elementId}", exceptions)
        {
            ObjectId = objectId;
            ElementId = elementId;
        }

        public long ObjectId { get; }

        public long ElementId { get; }
    }
}

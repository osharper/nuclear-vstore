using System;
using System.Collections.Generic;

using NuClear.VStore.Objects.ContentValidation.Errors;

namespace NuClear.VStore.Objects.ContentValidation
{
    public sealed class InvalidObjectElementException : Exception
    {
        public InvalidObjectElementException(long objectId, long elementId, IReadOnlyCollection<ObjectElementValidationError> errors)
        {
            ObjectId = objectId;
            ElementId = elementId;
            Errors = errors;
        }

        public long ObjectId { get; }

        public long ElementId { get; }

        public IReadOnlyCollection<ObjectElementValidationError> Errors { get; }
    }
}

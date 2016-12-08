using System;
using System.Collections.Generic;

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class UnsupportedAttributesException : Exception
    {
        public UnsupportedAttributesException(IReadOnlyCollection<string> attributes) :
            base($"Next unsupported attributes found: {string.Join(", ", attributes)}")
        {
            UnsupportedAttributes = attributes;
        }

        public IReadOnlyCollection<string> UnsupportedAttributes { get; }
    }
}

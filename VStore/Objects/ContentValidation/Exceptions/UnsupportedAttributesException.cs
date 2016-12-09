using System.Collections.Generic;

namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class UnsupportedAttributesException : ObjectElementValidationException
    {
        public UnsupportedAttributesException(IReadOnlyCollection<string> attributes) :
            base($"Next unsupported attributes found: {string.Join(", ", attributes)}")
        {
            UnsupportedAttributes = attributes;
        }

        public IReadOnlyCollection<string> UnsupportedAttributes { get; }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.UnsupportedAttributes;
    }
}

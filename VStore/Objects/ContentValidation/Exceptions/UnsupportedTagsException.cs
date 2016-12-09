using System.Collections.Generic;

namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class UnsupportedTagsException : ObjectElementValidationException
    {
        public UnsupportedTagsException(IReadOnlyCollection<string> supportedTags, IReadOnlyCollection<string> unsupportedTags)
        {
            SupportedTags = supportedTags;
            UnsupportedTags = unsupportedTags;
        }

        public IReadOnlyCollection<string> SupportedTags { get; }

        public IReadOnlyCollection<string> UnsupportedTags { get; }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.UnsupportedTags;
    }
}

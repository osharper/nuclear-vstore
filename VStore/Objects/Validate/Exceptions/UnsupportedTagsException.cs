using System;
using System.Collections.Generic;

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class UnsupportedTagsException : Exception
    {
        public UnsupportedTagsException(IReadOnlyCollection<string> supportedTags, IReadOnlyCollection<string> unsupportedTags) :
            base($"Found unsupported tags: {string.Join(", ", unsupportedTags)}")
        {
            SupportedTags = supportedTags;
            UnsupportedTags = unsupportedTags;
        }

        public IReadOnlyCollection<string> SupportedTags { get; }

        public IReadOnlyCollection<string> UnsupportedTags { get; }
    }
}

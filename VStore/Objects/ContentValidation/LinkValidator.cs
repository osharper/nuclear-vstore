using System;
using System.Collections.Generic;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Errors;

namespace NuClear.VStore.Objects.ContentValidation
{
    public static class LinkValidator
    {
        public static IEnumerable<ObjectElementValidationError> CheckLink(IObjectElementValue value, IElementConstraints constraints)
        {
            if (string.IsNullOrEmpty(value.Raw))
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            // We can use Uri.IsWellFormedUriString() instead:
            Uri uri;
            if (!Uri.TryCreate(value.Raw, UriKind.Absolute, out uri)
                || (uri.Scheme != "http" && uri.Scheme != "https")
                || uri.HostNameType != UriHostNameType.Dns)
            {
                return new[] { new IncorrectLinkError() };
            }

            return Array.Empty<ObjectElementValidationError>();
        }
    }
}

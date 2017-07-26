using System;
using System.Collections.Generic;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Errors;

namespace NuClear.VStore.Objects.ContentValidation
{
    public static class LinkValidator
    {
        public static IEnumerable<ObjectElementValidationError> CheckLink(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var rawValue = (IObjectElementRawValue)value;
            if (string.IsNullOrEmpty(rawValue.Raw))
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var constraints = (ILinkElementConstraints)elementConstraints;
            if (!constraints.ValidLink)
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            // We can use Uri.IsWellFormedUriString() instead:
            if (!Uri.TryCreate(rawValue.Raw, UriKind.Absolute, out var uri)
                || (uri.Scheme != "http" && uri.Scheme != "https")
                || uri.HostNameType != UriHostNameType.Dns)
            {
                return new[] { new IncorrectLinkError() };
            }

            return Array.Empty<ObjectElementValidationError>();
        }
    }
}

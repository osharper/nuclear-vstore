using System;
using System.Collections.Generic;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Objects.Validate.Exceptions;

namespace NuClear.VStore.Objects.Validate
{
    public static class LinkValidator
    {
        public static IEnumerable<Exception> CorrectLink(IObjectElementDescriptor descriptor, Language language)
        {
            if (string.IsNullOrEmpty(descriptor.Value.Raw))
            {
                return Array.Empty<Exception>();
            }

            // We can use Uri.IsWellFormedUriString() instead:
            Uri uri;
            if (!Uri.TryCreate(descriptor.Value.Raw, UriKind.Absolute, out uri)
                || (uri.Scheme != "http" && uri.Scheme != "https")
                || uri.HostNameType != UriHostNameType.Dns)
            {
                return new[] { new IncorrectLinkException() };
            }

            return Array.Empty<Exception>();
        }
    }
}

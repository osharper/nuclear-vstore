using System;

using NuClear.VStore.Descriptors;

namespace NuClear.VStore.Templates
{
    public sealed class ConstraintsNotFoundException : Exception
    {
        public ConstraintsNotFoundException(Language language)
            : base($"Constraints for language {language} nor for {Language.Unspecified} were not found.")
        {
        }
    }
}
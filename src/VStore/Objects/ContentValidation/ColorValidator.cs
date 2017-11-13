using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Errors;

namespace NuClear.VStore.Objects.ContentValidation
{
    public static class ColorValidator
    {
        private static readonly Regex HexRGBColorRegex = new Regex("^#[0-9A-F]{6}$", RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        public static IEnumerable<ObjectElementValidationError> CheckValidColor(IObjectElementValue value, IElementConstraints constraints)
        {
            var color = (ColorElementValue)value;
            return string.IsNullOrEmpty(color.Raw) || HexRGBColorRegex.IsMatch(color.Raw)
                       ? Array.Empty<ObjectElementValidationError>()
                       : new[] { new InvalidColorFormatError() };
        }
    }
}
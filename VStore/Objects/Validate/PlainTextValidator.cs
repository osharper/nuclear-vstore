using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Objects.Validate
{
    public static class PlainTextValidator
    {
        public static IEnumerable<Exception> CheckLength(IObjectElementDescriptor descriptor)
        {
            if (string.IsNullOrEmpty(descriptor?.Value?.Raw))
            {
                return Array.Empty<Exception>();
            }

            var constraints = (TextElementConstraints)descriptor.Constraints;
            if (!constraints.MaxSymbols.HasValue)
            {
                return Array.Empty<Exception>();
            }

            var textLength = TextValidationUtils.GetTextLength(descriptor.Value.Raw);

            return textLength > constraints.MaxSymbols.Value
                ? new[] { new ElementTextTooLongException() }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckWordsLength(IObjectElementDescriptor descriptor)
        {
            if (string.IsNullOrEmpty(descriptor?.Value?.Raw))
            {
                return Array.Empty<Exception>();
            }

            var constraints = (TextElementConstraints)descriptor.Constraints;
            if (!constraints.MaxSymbolsPerWord.HasValue)
            {
                return Array.Empty<Exception>();
            }

            var tooLongWords = TextValidationUtils.GetTooLongWords(descriptor.Value.Raw, constraints.MaxSymbolsPerWord.Value, new[] { ' ', '-', '/', '\n', '\t' });

            return tooLongWords.Count > 0
                ? new[] { new ElementTextTooLongException() }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckLinesCount(IObjectElementDescriptor descriptor)
        {
            if (string.IsNullOrEmpty(descriptor?.Value?.Raw))
            {
                return Array.Empty<Exception>();
            }

            var constraints = (TextElementConstraints)descriptor.Constraints;
            if (!constraints.MaxLines.HasValue)
            {
                return Array.Empty<Exception>();
            }

            var lineBreaks = Regex.Matches(descriptor.Value.Raw, "\n", RegexOptions.IgnoreCase).Count;

            return lineBreaks + 1 > constraints.MaxLines.Value
                ? new[] { new TooManyLinesException() }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckRestrictedSymbols(IObjectElementDescriptor descriptor)
        {
            return string.IsNullOrEmpty(descriptor?.Value?.Raw)
                ? Array.Empty<Exception>()
                : TextValidationUtils.CheckRestrictedSymbols(descriptor.Value.Raw);
        }
    }
}

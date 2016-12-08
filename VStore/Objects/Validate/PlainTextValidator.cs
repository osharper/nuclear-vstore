using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.Validate.Exceptions;

namespace NuClear.VStore.Objects.Validate
{
    public static class PlainTextValidator
    {
        public static IEnumerable<Exception> CheckLength(IObjectElementDescriptor descriptor, Language language)
        {
            if (string.IsNullOrEmpty(descriptor?.Value?.Raw))
            {
                return Array.Empty<Exception>();
            }

            var constraints = (TextElementConstraints)descriptor.Constraints.For(language);
            if (!constraints.MaxSymbols.HasValue)
            {
                return Array.Empty<Exception>();
            }

            var textLength = TextValidationUtils.GetTextLength(descriptor.Value.Raw);

            return textLength > constraints.MaxSymbols.Value
                ? new[] { new ElementTextTooLongException(constraints.MaxSymbols.Value, textLength) }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckWordsLength(IObjectElementDescriptor descriptor, Language language)
        {
            if (string.IsNullOrEmpty(descriptor?.Value?.Raw))
            {
                return Array.Empty<Exception>();
            }

            var constraints = (TextElementConstraints)descriptor.Constraints.For(language);
            if (!constraints.MaxSymbolsPerWord.HasValue)
            {
                return Array.Empty<Exception>();
            }

            var tooLongWords = TextValidationUtils.GetTooLongWords(descriptor.Value.Raw, constraints.MaxSymbolsPerWord.Value, new[] { ' ', '-', '/', '\n', '\t' });

            return tooLongWords.Count > 0
                ? new[] { new ElementWordsTooLongException(constraints.MaxSymbolsPerWord.Value, tooLongWords) }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckLinesCount(IObjectElementDescriptor descriptor, Language language)
        {
            if (string.IsNullOrEmpty(descriptor?.Value?.Raw))
            {
                return Array.Empty<Exception>();
            }

            var constraints = (TextElementConstraints)descriptor.Constraints.For(language);
            if (!constraints.MaxLines.HasValue)
            {
                return Array.Empty<Exception>();
            }

            var linesCount = 1 + Regex.Matches(descriptor.Value.Raw, "\n", RegexOptions.IgnoreCase).Count;

            return linesCount > constraints.MaxLines.Value
                ? new[] { new TooManyLinesException(constraints.MaxLines.Value, linesCount) }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckRestrictedSymbols(IObjectElementDescriptor descriptor, Language language)
        {
            return string.IsNullOrEmpty(descriptor?.Value?.Raw)
                ? Array.Empty<Exception>()
                : TextValidationUtils.CheckRestrictedSymbols(descriptor.Value.Raw);
        }
    }
}

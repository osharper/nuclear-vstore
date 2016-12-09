using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Exceptions;

namespace NuClear.VStore.Objects.ContentValidation
{
    public static class PlainTextValidator
    {
        private static readonly Func<IObjectElementValue, string> TextValueExtractor =
            value => (value as FasElementValue)?.Text ?? (value as TextElementValue)?.Raw;

        public static IEnumerable<Exception> CheckLength(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<Exception>();
            }

            var constraints = (TextElementConstraints)elementConstraints;
            if (!constraints.MaxSymbols.HasValue)
            {
                return Array.Empty<Exception>();
            }

            var textLength = TextValidationUtils.GetTextLength(textValue);

            return textLength > constraints.MaxSymbols.Value
                ? new[] { new ElementTextTooLongException(constraints.MaxSymbols.Value, textLength) }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckWordsLength(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<Exception>();
            }

            var constraints = (TextElementConstraints)elementConstraints;
            if (!constraints.MaxSymbolsPerWord.HasValue)
            {
                return Array.Empty<Exception>();
            }

            var tooLongWords = TextValidationUtils.GetTooLongWords(textValue, constraints.MaxSymbolsPerWord.Value, new[] { ' ', '-', '/', '\n', '\t' });

            return tooLongWords.Count > 0
                ? new[] { new ElementWordsTooLongException(constraints.MaxSymbolsPerWord.Value, tooLongWords) }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckLinesCount(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<Exception>();
            }

            var constraints = (TextElementConstraints)elementConstraints;
            if (!constraints.MaxLines.HasValue)
            {
                return Array.Empty<Exception>();
            }

            var linesCount = 1 + Regex.Matches(textValue, "\n", RegexOptions.IgnoreCase).Count;

            return linesCount > constraints.MaxLines.Value
                ? new[] { new TooManyLinesException(constraints.MaxLines.Value, linesCount) }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckRestrictedSymbols(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            return string.IsNullOrEmpty(textValue)
                ? Array.Empty<Exception>()
                : TextValidationUtils.CheckRestrictedSymbols(textValue);
        }
    }
}

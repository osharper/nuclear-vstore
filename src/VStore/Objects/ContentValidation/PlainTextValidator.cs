using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Errors;

namespace NuClear.VStore.Objects.ContentValidation
{
    public static class PlainTextValidator
    {
        private static readonly Func<IObjectElementValue, string> TextValueExtractor =
            value => value is FasElementValue ? ((FasElementValue)value).Text : ((TextElementValue)value).Raw;

        public static IEnumerable<ObjectElementValidationError> CheckLength(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var constraints = (PlainTextElementConstraints)elementConstraints;
            if (!constraints.MaxSymbols.HasValue)
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var textLength = TextValidationUtils.GetTextLength(textValue);

            return textLength > constraints.MaxSymbols.Value
                ? new[] { new ElementTextTooLongError(constraints.MaxSymbols.Value, textLength) }
                : Array.Empty<ObjectElementValidationError>();
        }

        public static IEnumerable<ObjectElementValidationError> CheckWordsLength(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var constraints = (PlainTextElementConstraints)elementConstraints;
            if (!constraints.MaxSymbolsPerWord.HasValue)
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var tooLongWords = TextValidationUtils.GetTooLongWords(textValue, constraints.MaxSymbolsPerWord.Value, new[] { ' ', '-', '/', '\n', '\t' });

            return tooLongWords.Count > 0
                ? new[] { new ElementWordsTooLongError(constraints.MaxSymbolsPerWord.Value, tooLongWords) }
                : Array.Empty<ObjectElementValidationError>();
        }

        public static IEnumerable<ObjectElementValidationError> CheckLinesCount(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var constraints = (PlainTextElementConstraints)elementConstraints;
            if (!constraints.MaxLines.HasValue)
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var linesCount = 1 + Regex.Matches(textValue, "\n", RegexOptions.IgnoreCase).Count;

            return linesCount > constraints.MaxLines.Value
                ? new[] { new TooManyLinesError(constraints.MaxLines.Value, linesCount) }
                : Array.Empty<ObjectElementValidationError>();
        }

        public static IEnumerable<ObjectElementValidationError> CheckRestrictedSymbols(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            var constraints = (PlainTextElementConstraints)elementConstraints;
            return string.IsNullOrEmpty(textValue)
                ? Array.Empty<ObjectElementValidationError>()
                : TextValidationUtils.CheckRestrictedSymbols(textValue, constraints);
        }
    }
}

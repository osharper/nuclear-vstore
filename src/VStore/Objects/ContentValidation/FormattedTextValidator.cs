using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using HtmlAgilityPack;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Errors;
using NuClear.VStore.Objects.ContentValidation.FormattedUtils;

namespace NuClear.VStore.Objects.ContentValidation
{
    public static class FormattedTextValidator
    {
        private static readonly Func<IObjectElementValue, string> TextValueExtractor =
            value => ((TextElementValue)value).Raw;

        public static IEnumerable<ObjectElementValidationError> CheckLength(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var constraints = (FormattedTextElementConstraints)elementConstraints;
            if (!constraints.MaxSymbols.HasValue)
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var textLength = new LengthCounter()
                .GetLength(textValue);

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

            var constraints = (FormattedTextElementConstraints)elementConstraints;
            if (!constraints.MaxSymbolsPerWord.HasValue)
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var tooLongWords = new LongWordsSearcher(constraints.MaxSymbolsPerWord.Value)
                .GetLongWords(textValue);

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

            var constraints = (FormattedTextElementConstraints)elementConstraints;
            if (!constraints.MaxLines.HasValue)
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var linesCount = new LinesCounter()
                .CountLines(textValue);

            return linesCount > constraints.MaxLines.Value
                ? new[] { new TooManyLinesError(constraints.MaxLines.Value, linesCount) }
                : Array.Empty<ObjectElementValidationError>();
        }

        public static IEnumerable<ObjectElementValidationError> CheckRestrictedSymbols(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            var constraints = (FormattedTextElementConstraints)elementConstraints;
            return string.IsNullOrEmpty(textValue)
                ? Array.Empty<ObjectElementValidationError>()
                : TextValidationUtils.CheckRestrictedSymbols(WebUtility.HtmlDecode(textValue), constraints);
        }

        public static IEnumerable<ObjectElementValidationError> CheckValidHtml(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var constraints = (FormattedTextElementConstraints)elementConstraints;
            if (!constraints.ValidHtml)
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var html = new HtmlDocument();
            html.LoadHtml(textValue);

            return html.ParseErrors.Any()
                ? new[] { new InvalidHtmlError() }
                : Array.Empty<ObjectElementValidationError>();
        }

        public static IEnumerable<ObjectElementValidationError> CheckSupportedHtmlTags(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var constraints = (FormattedTextElementConstraints)elementConstraints;
            var unsupportedTags = new HtmlTagsSearcher(constraints.SupportedTags)
                .GetUnsupportedTags(textValue);

            return unsupportedTags.Count > 0
                ? new[] { new UnsupportedTagsError(constraints.SupportedTags, unsupportedTags) }
                : Array.Empty<ObjectElementValidationError>();
        }

        public static IEnumerable<ObjectElementValidationError> CheckAttributesAbsence(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var attributes = new AttributesSearcher()
                .GetAttributes(textValue);

            var constraints = (FormattedTextElementConstraints)elementConstraints;
            var unsupportedAttributes = new HashSet<string>(attributes);
            unsupportedAttributes.ExceptWith(constraints.SupportedAttributes);

            return unsupportedAttributes.Count > 0
                ? new[] { new UnsupportedAttributesError(unsupportedAttributes) }
                : Array.Empty<ObjectElementValidationError>();
        }

        public static IEnumerable<ObjectElementValidationError> CheckEmptyList(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var constraints = (FormattedTextElementConstraints)elementConstraints;
            if (!constraints.NoEmptyLists)
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var emptyListFound = new EmptyListSearcher()
                .IsThereEmptyList(textValue);

            return emptyListFound
                ? new[] { new EmptyListError() }
                : Array.Empty<ObjectElementValidationError>();
        }

        public static IEnumerable<ObjectElementValidationError> CheckNestedList(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var constraints = (FormattedTextElementConstraints)elementConstraints;
            if (!constraints.NoNestedLists)
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var nestedListFound = new NestedListSearcher()
                .IsThereNestedList(textValue);

            return nestedListFound
                ? new[] { new NestedListError() }
                : Array.Empty<ObjectElementValidationError>();
        }

        public static IEnumerable<ObjectElementValidationError> CheckUnsupportedListElements(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationError>();
            }

            var constraints = (FormattedTextElementConstraints)elementConstraints;
            var unsupportedElementFound = new UnsupportedListElementsSearcher(constraints.SupportedListElements)
                .IsThereUnsupportedElement(textValue);

            return unsupportedElementFound
                ? new[] { new UnsupportedListElementsError() }
                : Array.Empty<ObjectElementValidationError>();
        }
    }
}

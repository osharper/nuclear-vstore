using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using HtmlAgilityPack;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Exceptions;
using NuClear.VStore.Objects.ContentValidation.FormattedUtils;

namespace NuClear.VStore.Objects.ContentValidation
{
    public static class FormattedTextValidator
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

            var textLength = new LengthCounter()
                .GetLength(textValue);

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

            var tooLongWords = new LongWordsSearcher(constraints.MaxSymbolsPerWord.Value)
                .GetLongWords(textValue);

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

            var linesCount = new LinesCounter()
                .CountLines(textValue);

            return linesCount > constraints.MaxLines.Value
                ? new[] { new TooManyLinesException(constraints.MaxLines.Value, linesCount) }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckRestrictedSymbols(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            return string.IsNullOrEmpty(textValue)
                ? Array.Empty<Exception>()
                : TextValidationUtils.CheckRestrictedSymbols(WebUtility.HtmlDecode(textValue));
        }

        public static IEnumerable<Exception> CheckValidHtml(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<Exception>();
            }

            var html = new HtmlDocument();
            html.LoadHtml(textValue);

            return html.ParseErrors.Any()
                ? new[] { new InvalidHtmlException() }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckSupportedHtmlTags(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<Exception>();
            }

            var supportedTags = new[]
                                    {
                                        ElementFormattedTextTagNames.Break,
                                        ElementFormattedTextTagNames.UnorderedList,
                                        ElementFormattedTextTagNames.ListItem,
                                        ElementFormattedTextTagNames.Strong,
                                        ElementFormattedTextTagNames.Bold,
                                        ElementFormattedTextTagNames.Emphasis,
                                        ElementFormattedTextTagNames.Italic
                                    };

            var unsupportedTags = new HtmlTagsSearcher(supportedTags)
                .GetUnsupportedTags(textValue);

            return unsupportedTags.Count > 0
                ? new[] { new UnsupportedTagsException(supportedTags, unsupportedTags) }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckAttributesAbsence(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<Exception>();
            }

            var attributes = new AttributesSearcher()
                .GetAttributes(textValue);

            return attributes.Count > 0
                ? new[] { new UnsupportedAttributesException(attributes) }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckEmptyList(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<Exception>();
            }

            var emptyListFound = new EmptyListSearcher()
                .IsThereEmptyList(textValue);

            return emptyListFound
                ? new[] { new EmptyListException() }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckNestedList(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<Exception>();
            }

            var nestedListFound = new NestedListSearcher()
                .IsThereNestedList(textValue);

            return nestedListFound
                ? new[] { new NestedListException() }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckUnsupportedListElements(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<Exception>();
            }

            var unsupportedElementFound = new UnsupportedListElementsSearcher()
                .IsThereUnsupportedElement(textValue);

            return unsupportedElementFound
                ? new[] { new UnsupportedListElementsException() }
                : Array.Empty<Exception>();
        }
    }
}

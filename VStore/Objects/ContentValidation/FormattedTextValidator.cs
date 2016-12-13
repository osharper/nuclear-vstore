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
            value => value is FasElementValue ? ((FasElementValue)value).Text : ((TextElementValue)value).Raw;

        public static IEnumerable<ObjectElementValidationException> CheckLength(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationException>();
            }

            var constraints = (TextElementConstraints)elementConstraints;
            if (!constraints.MaxSymbols.HasValue)
            {
                return Array.Empty<ObjectElementValidationException>();
            }

            var textLength = new LengthCounter()
                .GetLength(textValue);

            return textLength > constraints.MaxSymbols.Value
                ? new[] { new ElementTextTooLongException(constraints.MaxSymbols.Value, textLength) }
                : Array.Empty<ObjectElementValidationException>();
        }

        public static IEnumerable<ObjectElementValidationException> CheckWordsLength(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationException>();
            }

            var constraints = (TextElementConstraints)elementConstraints;
            if (!constraints.MaxSymbolsPerWord.HasValue)
            {
                return Array.Empty<ObjectElementValidationException>();
            }

            var tooLongWords = new LongWordsSearcher(constraints.MaxSymbolsPerWord.Value)
                .GetLongWords(textValue);

            return tooLongWords.Count > 0
                ? new[] { new ElementWordsTooLongException(constraints.MaxSymbolsPerWord.Value, tooLongWords) }
                : Array.Empty<ObjectElementValidationException>();
        }

        public static IEnumerable<ObjectElementValidationException> CheckLinesCount(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationException>();
            }

            var constraints = (TextElementConstraints)elementConstraints;
            if (!constraints.MaxLines.HasValue)
            {
                return Array.Empty<ObjectElementValidationException>();
            }

            var linesCount = new LinesCounter()
                .CountLines(textValue);

            return linesCount > constraints.MaxLines.Value
                ? new[] { new TooManyLinesException(constraints.MaxLines.Value, linesCount) }
                : Array.Empty<ObjectElementValidationException>();
        }

        public static IEnumerable<ObjectElementValidationException> CheckRestrictedSymbols(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            return string.IsNullOrEmpty(textValue)
                ? Array.Empty<ObjectElementValidationException>()
                : TextValidationUtils.CheckRestrictedSymbols(WebUtility.HtmlDecode(textValue));
        }

        public static IEnumerable<ObjectElementValidationException> CheckValidHtml(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationException>();
            }

            var html = new HtmlDocument();
            html.LoadHtml(textValue);

            return html.ParseErrors.Any()
                ? new[] { new InvalidHtmlException() }
                : Array.Empty<ObjectElementValidationException>();
        }

        public static IEnumerable<ObjectElementValidationException> CheckSupportedHtmlTags(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationException>();
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
                : Array.Empty<ObjectElementValidationException>();
        }

        public static IEnumerable<ObjectElementValidationException> CheckAttributesAbsence(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationException>();
            }

            var attributes = new AttributesSearcher()
                .GetAttributes(textValue);

            return attributes.Count > 0
                ? new[] { new UnsupportedAttributesException(attributes) }
                : Array.Empty<ObjectElementValidationException>();
        }

        public static IEnumerable<ObjectElementValidationException> CheckEmptyList(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationException>();
            }

            var emptyListFound = new EmptyListSearcher()
                .IsThereEmptyList(textValue);

            return emptyListFound
                ? new[] { new EmptyListException() }
                : Array.Empty<ObjectElementValidationException>();
        }

        public static IEnumerable<ObjectElementValidationException> CheckNestedList(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationException>();
            }

            var nestedListFound = new NestedListSearcher()
                .IsThereNestedList(textValue);

            return nestedListFound
                ? new[] { new NestedListException() }
                : Array.Empty<ObjectElementValidationException>();
        }

        public static IEnumerable<ObjectElementValidationException> CheckUnsupportedListElements(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var textValue = TextValueExtractor(value);
            if (string.IsNullOrEmpty(textValue))
            {
                return Array.Empty<ObjectElementValidationException>();
            }

            var unsupportedElementFound = new UnsupportedListElementsSearcher()
                .IsThereUnsupportedElement(textValue);

            return unsupportedElementFound
                ? new[] { new UnsupportedListElementsException() }
                : Array.Empty<ObjectElementValidationException>();
        }
    }
}

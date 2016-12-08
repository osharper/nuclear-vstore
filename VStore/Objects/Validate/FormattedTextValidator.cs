using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

using HtmlAgilityPack;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.Validate.Exceptions;
using NuClear.VStore.Objects.Validate.FormattedUtils;

namespace NuClear.VStore.Objects.Validate
{
    public static class FormattedTextValidator
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

            var textLength = new LengthCounter()
                .GetLength(descriptor.Value.Raw);

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

            var tooLongWords = new LongWordsSearcher(constraints.MaxSymbolsPerWord.Value)
                .GetLongWords(descriptor.Value.Raw);

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

            var linesCount = new LinesCounter()
                .CountLines(descriptor.Value.Raw);

            return linesCount > constraints.MaxLines.Value
                ? new[] { new TooManyLinesException(constraints.MaxLines.Value, linesCount) }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckRestrictedSymbols(IObjectElementDescriptor descriptor, Language language)
        {
            return string.IsNullOrEmpty(descriptor?.Value?.Raw)
                ? Array.Empty<Exception>()
                : TextValidationUtils.CheckRestrictedSymbols(WebUtility.HtmlDecode(descriptor.Value.Raw));
        }

        public static IEnumerable<Exception> CheckValidHtml(IObjectElementDescriptor descriptor, Language language)
        {
            if (string.IsNullOrEmpty(descriptor?.Value?.Raw))
            {
                return Array.Empty<Exception>();
            }

            var html = new HtmlDocument();
            html.LoadHtml(descriptor.Value.Raw);

            return html.ParseErrors.Any()
                ? new[] { new InvalidHtmlException() }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckSupportedHtmlTags(IObjectElementDescriptor descriptor, Language language)
        {
            if (string.IsNullOrEmpty(descriptor?.Value?.Raw))
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
                .GetUnsupportedTags(descriptor.Value.Raw);

            return unsupportedTags.Count > 0
                ? new[] { new UnsupportedTagsException(supportedTags, unsupportedTags) }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckAttributesAbsence(IObjectElementDescriptor descriptor, Language language)
        {
            if (string.IsNullOrEmpty(descriptor?.Value?.Raw))
            {
                return Array.Empty<Exception>();
            }

            var attributes = new AttributesSearcher()
                .GetAttributes(descriptor.Value.Raw);

            return attributes.Count > 0
                ? new[] { new UnsupportedAttributesException(attributes) }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckEmptyList(IObjectElementDescriptor descriptor, Language language)
        {
            if (string.IsNullOrEmpty(descriptor?.Value?.Raw))
            {
                return Array.Empty<Exception>();
            }

            var emptyListFound = new EmptyListSearcher()
                .IsThereEmptyList(descriptor.Value.Raw);

            return emptyListFound
                ? new[] { new EmptyListException() }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckNestedList(IObjectElementDescriptor descriptor, Language language)
        {
            if (string.IsNullOrEmpty(descriptor?.Value?.Raw))
            {
                return Array.Empty<Exception>();
            }

            var nestedListFound = new NestedListSearcher()
                .IsThereNestedList(descriptor.Value.Raw);

            return nestedListFound
                ? new[] { new NestedListException() }
                : Array.Empty<Exception>();
        }

        public static IEnumerable<Exception> CheckUnsupportedListElements(IObjectElementDescriptor descriptor, Language language)
        {
            if (string.IsNullOrEmpty(descriptor?.Value?.Raw))
            {
                return Array.Empty<Exception>();
            }

            var unsupportedElementFound = new UnsupportedListElementsSearcher()
                .IsThereUnsupportedElement(descriptor.Value.Raw);

            return unsupportedElementFound
                ? new[] { new UnsupportedListElementsException() }
                : Array.Empty<Exception>();
        }
    }
}

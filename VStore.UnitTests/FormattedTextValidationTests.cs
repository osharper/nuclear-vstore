using System.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.Exceptions;

using Xunit;

// ReSharper disable UnusedMember.Global
namespace VStore.UnitTests
{
    public class FormattedTextValidationTests
    {
        private static readonly TestHelpers.Validator[] AllChecks =
            {
                FormattedTextValidator.CheckLength,
                FormattedTextValidator.CheckWordsLength,
                FormattedTextValidator.CheckLinesCount,
                FormattedTextValidator.CheckRestrictedSymbols,
                FormattedTextValidator.CheckValidHtml,
                FormattedTextValidator.CheckSupportedHtmlTags,
                FormattedTextValidator.CheckAttributesAbsence,
                FormattedTextValidator.CheckEmptyList,
                FormattedTextValidator.CheckNestedList,
                FormattedTextValidator.CheckUnsupportedListElements
            };

        [Fact]
        public void TestTextCheckLength()
        {
            const int MaxSymbols = 10;
            var value = new TextElementValue { Raw = new string('a', MaxSymbols) };
            var constraints = new TextElementConstraints { IsFormatted = true, MaxSymbols = MaxSymbols };

            var error = TestHelpers.MakeCheck<TextElementValue, ElementTextTooLongException>(value, constraints, FormattedTextValidator.CheckLength, val => val.Raw = new string('b', MaxSymbols + 1));
            Assert.StrictEqual(MaxSymbols, error.MaxLength);
            Assert.StrictEqual(value.Raw.Length, error.ActualLength);

            value.Raw = "<i><b>" + new string('a', MaxSymbols) + "</b></i>";
            error = TestHelpers.MakeCheck<TextElementValue, ElementTextTooLongException>(value, constraints, FormattedTextValidator.CheckLength, val => val.Raw = new string('b', MaxSymbols + 1));
            Assert.StrictEqual(MaxSymbols, error.MaxLength);
            Assert.StrictEqual(value.Raw.Length, error.ActualLength);
        }

        [Fact]
        public void TestTextCheckMarkup()
        {
            var value = new TextElementValue { Raw = "<br ><br/><br /><br><ul><li><b><i><strong>text</strong><i/></b></li><em>small</em><li></li></ul>" };
            var constraints = new TextElementConstraints { IsFormatted = true };

            TestHelpers.MakeCheck<TextElementValue, InvalidHtmlException>(value, constraints, FormattedTextValidator.CheckValidHtml, val => val.Raw = val.Raw + "<ul>");

            value.Raw = "<html><head></head><body></body></html>";
            var errors = FormattedTextValidator.CheckSupportedHtmlTags(value, constraints).ToList();
            Assert.StrictEqual(1, errors.Count);
            Assert.IsType<UnsupportedTagsException>(errors.First());
            var error = (UnsupportedTagsException)errors.First();
            Assert.StrictEqual(3, error.UnsupportedTags.Count);
            Assert.Contains("html", error.UnsupportedTags);
            Assert.Contains("head", error.UnsupportedTags);
            Assert.Contains("body", error.UnsupportedTags);
        }

        [Theory]
        [InlineData("Too long text", 1, null, null, false, 1)]
        [InlineData("<i><b>Too long text</b></i>", 1, null, null, false, 1)]
        [InlineData("Too_long_word", null, 1, null, false, 1)]
        [InlineData("<i><b>Too_long_word</b></i>", null, 1, null, false, 1)]
        [InlineData("Text <br> on <br/> too <br /> many \n lines", null, null, 3, false, 1)]
        [InlineData("Too_long_word_in_too_long_text", 1, 1, null, false, 2)]
        [InlineData("\r\v bad symbols and non breaking space \xA0", null, null, null, true, 2)]
        [InlineData("Too_long_word <br> on <br> too <br> many <br> lines", null, 1, 4, false, 2)]
        [InlineData("Too_long_word_in_too_long_text <br> on <br> too <br> many <br> lines", 1, 1, 4, false, 3)]
        [InlineData("Long_Word in too long text <br> with too many lines, \r\v bad symbols and non breaking space \xA0", 10, 5, 1)]
        public void TestAllChecks(string text, int? maxLength, int? maxWordLength, int? maxLines, bool containsRestrictedSymbols = true, int expectedErrorsCount = 5)
        {
            IObjectElementValue value = new TextElementValue { Raw = text };
            var constraints = new TextElementConstraints { IsFormatted = true, MaxLines = maxLines, MaxSymbols = maxLength, MaxSymbolsPerWord = maxWordLength };

            TestHelpers.InternalChecksTest(AllChecks, containsRestrictedSymbols, expectedErrorsCount, value, constraints);

            value = new FasElementValue { Raw = "custom", Text = text };
            TestHelpers.InternalChecksTest(AllChecks, containsRestrictedSymbols, expectedErrorsCount, value, constraints);
        }
    }
}

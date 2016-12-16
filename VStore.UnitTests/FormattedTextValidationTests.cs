using System;
using System.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.Errors;

using NUnit.Framework;

// ReSharper disable UnusedMember.Global
namespace VStore.UnitTests
{
    [TestFixture]
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

        [Test]
        public void TestTextCheckLength()
        {
            const int MaxSymbols = 10;
            var value = new TextElementValue { Raw = new string('a', MaxSymbols) };
            var constraints = new TextElementConstraints { IsFormatted = true, MaxSymbols = MaxSymbols };

            var error = TestHelpers.MakeCheck<TextElementValue, ElementTextTooLongError>(
                value,
                constraints,
                FormattedTextValidator.CheckLength,
                val => val.Raw = new string('b', MaxSymbols + 1));
            Assert.AreEqual(MaxSymbols, error.MaxLength);
            Assert.AreEqual(value.Raw.Length, error.ActualLength);

            value.Raw = "<i><b>" + new string('a', MaxSymbols) + "</b></i>";
            error = TestHelpers.MakeCheck<TextElementValue, ElementTextTooLongError>(
                value,
                constraints,
                FormattedTextValidator.CheckLength,
                val => val.Raw = "<i><b>" + new string('b', MaxSymbols + 1) + "</b></i>");
            Assert.AreEqual(MaxSymbols, error.MaxLength);
            Assert.AreEqual(MaxSymbols + 1, error.ActualLength);
            Assert.AreEqual(ElementValidationErrors.TextTooLong, error.ErrorType);
        }

        [Test]
        public void TestTextCheckLongWords()
        {
            const int MaxSymbols = 10;
            var value = new TextElementValue { Raw = new string('a', MaxSymbols) };
            var constraints = new TextElementConstraints { IsFormatted = true, MaxSymbolsPerWord = MaxSymbols };

            var error = TestHelpers.MakeCheck<TextElementValue, ElementWordsTooLongError>(
                value,
                constraints,
                FormattedTextValidator.CheckWordsLength,
                val => val.Raw = new string('b', MaxSymbols + 1));
            Assert.AreEqual(MaxSymbols, error.MaxWordLength);
            Assert.AreEqual(1, error.TooLongWords.Count);
            Assert.AreEqual(value.Raw, error.TooLongWords.First());
            Assert.AreEqual(ElementValidationErrors.WordsTooLong, error.ErrorType);
        }

        [Test]
        public void TestTextCheckMaxLines()
        {
            var value = new TextElementValue { Raw = "1 <br> 2 <br/> 3 <br /> 4 \n 4" };
            var constraints = new TextElementConstraints { IsFormatted = true, MaxLines = 4 };

            var error = TestHelpers.MakeCheck<TextElementValue, TooManyLinesError>(
                value,
                constraints,
                FormattedTextValidator.CheckLinesCount,
                val => val.Raw += "<ul><li> 5 </li></ul>");
            Assert.AreEqual(constraints.MaxLines, error.MaxLinesCount);
            Assert.AreEqual(constraints.MaxLines + 1, error.ActualLinesCount);
            Assert.AreEqual(ElementValidationErrors.TooManyLines, error.ErrorType);
        }

        [Test]
        public void TestTextCheckRestrictedSymbols()
        {
            const string AllChars = "abcdefghijklmnopqrstuvwxyz \n\t абвгдеёжзийклмнопрстуфхцчшщьыъэюя 1234567890 \\ \" .,;:~'`!? №@#$%^&|_ []{}()<> /*-+=";
            var value = new TextElementValue { Raw = AllChars };
            var constraints = new TextElementConstraints { IsFormatted = true };

            var errorOnSpace = TestHelpers.MakeCheck<TextElementValue, NonBreakingSpaceSymbolError>(
                value,
                constraints,
                FormattedTextValidator.CheckRestrictedSymbols,
                val => val.Raw = "\x00A0");
            Assert.AreEqual(ElementValidationErrors.NonBreakingSpaceSymbol, errorOnSpace.ErrorType);

            value.Raw = AllChars.ToUpper();
            var errorOnChars = TestHelpers.MakeCheck<TextElementValue, ControlСharactersInTextError>(
                value,
                constraints,
                FormattedTextValidator.CheckRestrictedSymbols,
                val => val.Raw = "\r");
            Assert.AreEqual(ElementValidationErrors.ControlСharacters, errorOnChars.ErrorType);
        }

        [Test]
        public void TestTextCheckMarkup()
        {
            var value = new TextElementValue { Raw = "<br/><br /><br><ul><li><b><i><strong> text &nbsp; </strong><i/></b></li><em>small</em><li></li></ul>" };
            var constraints = new TextElementConstraints { IsFormatted = true };

            var error = TestHelpers.MakeCheck<TextElementValue, InvalidHtmlError>(
                value,
                constraints,
                FormattedTextValidator.CheckValidHtml,
                val => val.Raw += "<ul>");
            Assert.AreEqual(ElementValidationErrors.InvalidHtml, error.ErrorType);
        }

        [Test]
        public void TestTextCheckTags()
        {
            var value = new TextElementValue { Raw = "<br/><br /><br><ul><li><b><i><strong> text &nbsp; </strong><i/></b></li><em>small</em><li></li></ul>" };
            var constraints = new TextElementConstraints { IsFormatted = true };

            var error = TestHelpers.MakeCheck<TextElementValue, UnsupportedTagsError>(
                value,
                constraints,
                FormattedTextValidator.CheckSupportedHtmlTags,
                val => val.Raw = "<html><head></head><body><p></p><hr></body></html>");
            Assert.AreEqual(5, error.UnsupportedTags.Count);
            Assert.That(error.UnsupportedTags, Is.EquivalentTo(new[] { "html", "head", "body", "hr", "p" }));
            Assert.AreEqual(ElementValidationErrors.UnsupportedTags, error.ErrorType);
        }

        [Test]
        public void TestTextCheckAttributes()
        {
            var value = new TextElementValue { Raw = "<b></b>" };
            var constraints = new TextElementConstraints { IsFormatted = true };

            var error = TestHelpers.MakeCheck<TextElementValue, UnsupportedAttributesError>(
                value,
                constraints,
                FormattedTextValidator.CheckAttributesAbsence,
                val => val.Raw = "<b class='err'><i onclick='alert(123)'></i></b>");

            Assert.AreEqual(2, error.UnsupportedAttributes.Count);
            Assert.That(error.UnsupportedAttributes, Is.EquivalentTo(new[] { "class", "onclick" }));
            Assert.AreEqual(ElementValidationErrors.UnsupportedAttributes, error.ErrorType);
        }

        [Test]
        public void TestTextCheckEmptyList()
        {
            var value = new TextElementValue { Raw = "<ul><li></li></ul>" };
            var constraints = new TextElementConstraints { IsFormatted = true };

            var error = TestHelpers.MakeCheck<TextElementValue, EmptyListError>(
                value,
                constraints,
                FormattedTextValidator.CheckEmptyList,
                val => val.Raw = "<ul> </ul>");

            Assert.AreEqual(ElementValidationErrors.EmptyList, error.ErrorType);
        }

        [Test]
        public void TestTextCheckNestedList()
        {
            var value = new TextElementValue { Raw = "<ul><li> list item </li></ul>" };
            var constraints = new TextElementConstraints { IsFormatted = true };

            var error = TestHelpers.MakeCheck<TextElementValue, NestedListError>(
                value,
                constraints,
                FormattedTextValidator.CheckNestedList,
                val => val.Raw = "<ul><li> outer list <ul><li> inner list </li></ul> </li></ul>");

            Assert.AreEqual(ElementValidationErrors.NestedList, error.ErrorType);
        }

        [Test]
        public void TestTextCheckListElements()
        {
            var value = new TextElementValue { Raw = "<ul><li> list item </li></ul>" };
            var constraints = new TextElementConstraints { IsFormatted = true };

            var error = TestHelpers.MakeCheck<TextElementValue, UnsupportedListElementsError>(
                value,
                constraints,
                FormattedTextValidator.CheckUnsupportedListElements,
                val => val.Raw = "<ul><hr></ul>");

            Assert.AreEqual(ElementValidationErrors.UnsupportedListElements, error.ErrorType);
        }

        [Test]
        [TestCase("Too long text", 1, null, null, false, 1)]
        [TestCase("<i><b>Too long text</b></i>", 1, null, null, false, 1)]
        [TestCase("Too_long_word", null, 1, null, false, 1)]
        [TestCase("<i><b>Too_long_word</b></i>", null, 1, null, false, 1)]
        [TestCase("Text <br> on <br/> too <br /> many \n lines", null, null, 3, false, 1)]
        [TestCase("Too_long_word_in_too_long_text", 1, 1, null, false, 2)]
        [TestCase("\r\v bad symbols and non breaking space \xA0", null, null, null, true, 2)]
        [TestCase("Too_long_word <br> on <br> too <br> many <br> lines", null, 1, 4, false, 2)]
        [TestCase("Too_long_word_in_too_long_text <br> on <br> too <br> many <br> lines", 1, 1, 4, false, 3)]
        [TestCase("Long_Word in too long text <br> with too many lines, \r\v bad symbols and non breaking space \xA0", 10, 5, 1)]
        public void TestAllChecks(string text, int? maxLength, int? maxWordLength, int? maxLines, bool containsRestrictedSymbols = true, int expectedErrorsCount = 5)
        {
            IObjectElementValue value = new TextElementValue { Raw = text };
            var constraints = new TextElementConstraints { IsFormatted = true, MaxLines = maxLines, MaxSymbols = maxLength, MaxSymbolsPerWord = maxWordLength };

            TestHelpers.InternalTextChecksTest(AllChecks, containsRestrictedSymbols, expectedErrorsCount, value, constraints);

            // FasComment cannot be formatted:
            value = new FasElementValue { Raw = "custom", Text = text };
            Assert.Throws<InvalidCastException>(() => TestHelpers.InternalTextChecksTest(AllChecks, containsRestrictedSymbols, expectedErrorsCount, value, constraints));
        }
    }
}

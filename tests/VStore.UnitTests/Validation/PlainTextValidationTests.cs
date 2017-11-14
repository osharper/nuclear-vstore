﻿using System.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.Errors;

using Xunit;

// ReSharper disable UnusedMember.Global
namespace VStore.UnitTests.Validation
{
    public class PlainTextValidationTests
    {
        private static readonly TestHelpers.Validator[] AllChecks =
            {
                PlainTextValidator.CheckLength,
                PlainTextValidator.CheckWordsLength,
                PlainTextValidator.CheckLinesCount,
                PlainTextValidator.CheckRestrictedSymbols
            };

        [Fact]
        public void TestTextCheckLength()
        {
            const int MaxSymbols = 50;
            var value = new TextElementValue { Raw = new string('a', MaxSymbols) };
            var constraints = new PlainTextElementConstraints { MaxSymbols = MaxSymbols };

            var error = TestHelpers.MakeValidationCheck<TextElementValue, ElementTextTooLongError>(
                value,
                constraints,
                PlainTextValidator.CheckLength,
                val => val.Raw = new string('b', MaxSymbols + 1));
            Assert.Equal(MaxSymbols, error.MaxLength);
            Assert.Equal(MaxSymbols + 1, error.ActualLength);
            Assert.Equal(nameof(constraints.MaxSymbols), error.ErrorType);
        }

        [Fact]
        public void TestTextCheckLongWords()
        {
            const int MaxSymbols = 10;
            var value = new TextElementValue { Raw = new string('a', MaxSymbols) };
            var constraints = new PlainTextElementConstraints { MaxSymbolsPerWord = MaxSymbols };

            var error = TestHelpers.MakeValidationCheck<TextElementValue, ElementWordsTooLongError>(
                value,
                constraints,
                PlainTextValidator.CheckWordsLength,
                val => val.Raw = new string('b', MaxSymbols + 1));
            Assert.Equal(MaxSymbols, error.MaxWordLength);
            Assert.Equal(1, error.TooLongWords.Count);
            Assert.Equal(value.Raw, error.TooLongWords.First());
            Assert.Equal(nameof(constraints.MaxSymbolsPerWord), error.ErrorType);
        }

        [Fact]
        public void TestTextCheckMaxLines()
        {
            const int MaxLines = 10;
            var value = new TextElementValue { Raw = new string('\n', MaxLines - 1) };
            var constraints = new PlainTextElementConstraints { MaxLines = MaxLines };

            var error = TestHelpers.MakeValidationCheck<TextElementValue, TooManyLinesError>(
                value,
                constraints,
                PlainTextValidator.CheckLinesCount,
                val => val.Raw = new string('\n', MaxLines));
            Assert.Equal(MaxLines, error.MaxLinesCount);
            Assert.Equal(MaxLines + 1, error.ActualLinesCount);
            Assert.Equal(nameof(constraints.MaxLines), error.ErrorType);
        }

        [Fact]
        public void TestTextCheckRestrictedSymbols()
        {
            const string AllChars = "abcdefghijklmnopqrstuvwxyz \n\t абвгдеёжзийклмнопрстуфхцчшщьыъэюя 1234567890 \\ \" .,;:~'`!? №@#$%^&_ []{}()<> /*-+=";
            var value = new TextElementValue { Raw = AllChars };
            var constraints = new PlainTextElementConstraints();

            var errorSpace = TestHelpers.MakeValidationCheck<TextElementValue, NonBreakingSpaceSymbolError>(
                value,
                constraints,
                PlainTextValidator.CheckRestrictedSymbols,
                val => val.Raw = "\x00A0");
            Assert.Equal(nameof(constraints.WithoutNonBreakingSpace), errorSpace.ErrorType);

            value.Raw = AllChars.ToUpper();
            var errorControlChars = TestHelpers.MakeValidationCheck<TextElementValue, ControlCharactersInTextError>(
                value,
                constraints,
                PlainTextValidator.CheckRestrictedSymbols,
                val => val.Raw = "\r");
            Assert.Equal(nameof(constraints.WithoutControlChars), errorControlChars.ErrorType);
        }

        [Fact]
        public void TestFasCommentCheckLength()
        {
            var value = new FasElementValue { Raw = "custom", Text = "text" };
            var constraints = new PlainTextElementConstraints { MaxSymbols = 5 };

            var error = TestHelpers.MakeValidationCheck<FasElementValue, ElementTextTooLongError>(
                value,
                constraints,
                PlainTextValidator.CheckLength,
                val => val.Text = "long text");
            Assert.Equal(constraints.MaxSymbols, error.MaxLength);
            Assert.Equal(value.Text.Length, error.ActualLength);
            Assert.Equal(nameof(constraints.MaxSymbols), error.ErrorType);
        }

        [Fact]
        public void TestFasCommentCheckLongWords()
        {
            const int MaxSymbols = 4;
            var value = new FasElementValue { Raw = "custom", Text = new string('a', MaxSymbols) };
            var constraints = new PlainTextElementConstraints { MaxSymbolsPerWord = MaxSymbols };

            var error = TestHelpers.MakeValidationCheck<FasElementValue, ElementWordsTooLongError>(
                value,
                constraints,
                PlainTextValidator.CheckWordsLength,
                val => val.Text = new string('b', MaxSymbols + 1));
            Assert.Equal(MaxSymbols, error.MaxWordLength);
            Assert.Equal(1, error.TooLongWords.Count);
            Assert.Equal(value.Text, error.TooLongWords.First());
            Assert.Equal(nameof(constraints.MaxSymbolsPerWord), error.ErrorType);
        }

        [Fact]
        public void TestFasCommentCheckMaxLines()
        {
            const int MaxLines = 10;
            var value = new FasElementValue { Raw = "custom", Text = new string('\n', MaxLines - 1) };
            var constraints = new PlainTextElementConstraints { MaxLines = MaxLines };

            var error = TestHelpers.MakeValidationCheck<FasElementValue, TooManyLinesError>(
                value,
                constraints,
                PlainTextValidator.CheckLinesCount,
                val => val.Text = new string('\n', MaxLines));
            Assert.Equal(MaxLines, error.MaxLinesCount);
            Assert.Equal(MaxLines + 1, error.ActualLinesCount);
            Assert.Equal(nameof(constraints.MaxLines), error.ErrorType);
        }

        [Fact]
        public void TestFasCommentCheckRestrictedSymbols()
        {
            const string AllChars = "abcdefghijklmnopqrstuvwxyz \n\t абвгдеёжзийклмнопрстуфхцчшщьыъэюя 1234567890 \\ \" .,;:~'`!? №@#$%^&|_ []{}()<> /*-+=";
            var value = new FasElementValue { Raw = "custom", Text = AllChars };
            var constraints = new PlainTextElementConstraints();

            TestHelpers.MakeValidationCheck<FasElementValue, NonBreakingSpaceSymbolError>(value, constraints, PlainTextValidator.CheckRestrictedSymbols, val => val.Text = "\x00A0");

            value.Text = AllChars.ToUpper();
            TestHelpers.MakeValidationCheck<FasElementValue, ControlCharactersInTextError>(value, constraints, PlainTextValidator.CheckRestrictedSymbols, val => val.Text = "\r");
        }

        [Theory]
        [InlineData("Too long text", 1, null, null, false, 1)]
        [InlineData("Too_long_word", null, 1, null, false, 1)]
        [InlineData("Text \n on \n too \n many \n lines", null, null, 4, false, 1)]
        [InlineData("Too_long_word_in_too_long_text", 1, 1, null, false, 2)]
        [InlineData("\r\v bad symbols and non breaking space \xA0", null, null, null, true, 2)]
        [InlineData("Too_long_word \n on \n too \n many \n lines", null, 1, 4, false, 2)]
        [InlineData("Too_long_word_in_too_long_text \n on \n too \n many \n lines", 1, 1, 4, false, 3)]
        [InlineData("Long_Word in too long text \n with too many lines, \r\v bad symbols and non breaking space \xA0", 10, 5, 1)]
        public void TestAllChecks(string text, int? maxLength, int? maxWordLength, int? maxLines, bool containsRestrictedSymbols = true, int expectedErrorsCount = 5)
        {
            IObjectElementValue value = new TextElementValue { Raw = text };
            var constraints = new PlainTextElementConstraints { MaxLines = maxLines, MaxSymbols = maxLength, MaxSymbolsPerWord = maxWordLength };

            TestHelpers.InternalTextChecksTest(AllChecks, containsRestrictedSymbols, expectedErrorsCount, value, constraints);

            value = new FasElementValue { Raw = "custom", Text = text };
            TestHelpers.InternalTextChecksTest(AllChecks, containsRestrictedSymbols, expectedErrorsCount, value, constraints);
        }
    }
}

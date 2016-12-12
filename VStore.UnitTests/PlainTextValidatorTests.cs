using System;
using System.Collections.Generic;
using System.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.Exceptions;

using Xunit;

// ReSharper disable UnusedMember.Global
namespace VStore.UnitTests
{
    public class PlainTextValidatorTests
    {
        private static readonly Validator[] AllChecks =
            {
                PlainTextValidator.CheckLength,
                PlainTextValidator.CheckWordsLength,
                PlainTextValidator.CheckLinesCount,
                PlainTextValidator.CheckRestrictedSymbols
            };

        private delegate IEnumerable<Exception> Validator(IObjectElementValue value, IElementConstraints elementConstraints);

        [Fact]
        public void TestTextCheckLength()
        {
            const int MaxSymbols = 50;
            var value = new TextElementValue { Raw = new string('a', MaxSymbols) };
            var constraints = new TextElementConstraints { IsFormatted = false, MaxSymbols = MaxSymbols };

            var error = MakeCheck<TextElementValue, ElementTextTooLongException>(value, constraints, PlainTextValidator.CheckLength, val => val.Raw = new string('b', MaxSymbols + 1));
            Assert.StrictEqual(MaxSymbols, error.MaxLength);
            Assert.StrictEqual(value.Raw.Length, error.ActualLength);
        }

        [Fact]
        public void TestTextCheckLongWords()
        {
            const int MaxSymbols = 10;
            var value = new TextElementValue { Raw = new string('a', MaxSymbols) };
            var constraints = new TextElementConstraints { IsFormatted = false, MaxSymbolsPerWord = MaxSymbols };

            var error = MakeCheck<TextElementValue, ElementWordsTooLongException>(value, constraints, PlainTextValidator.CheckWordsLength, val => val.Raw = new string('b', MaxSymbols + 1));
            Assert.StrictEqual(MaxSymbols, error.MaxWordLength);
            Assert.StrictEqual(1, error.TooLongWords.Count);
            Assert.StrictEqual(value.Raw, error.TooLongWords.First());
        }

        [Fact]
        public void TestTextCheckMaxLines()
        {
            const int MaxLines = 10;
            var value = new TextElementValue { Raw = new string('\n', MaxLines - 1) };
            var constraints = new TextElementConstraints { IsFormatted = false, MaxLines = MaxLines };

            var error = MakeCheck<TextElementValue, TooManyLinesException>(value, constraints, PlainTextValidator.CheckLinesCount, val => val.Raw = new string('\n', MaxLines));
            Assert.StrictEqual(MaxLines, error.MaxLinesCount);
            Assert.StrictEqual(MaxLines + 1, error.ActualLinesCount);
        }

        [Fact]
        public void TestTextCheckRestrictedSymbols()
        {
            const string AllChars = "abcdefghijklmnopqrstuvwxyz \n\t абвгдеёжзийклмнопрстуфхцчшщьыъэюя 1234567890 \\ \" .,;:~'`!? №@#$%^&_ []{}()<> /*-+=";
            var value = new TextElementValue { Raw = AllChars };
            var constraints = new TextElementConstraints { IsFormatted = false };

            MakeCheck<TextElementValue, NonBreakingSpaceSymbolException>(value, constraints, PlainTextValidator.CheckRestrictedSymbols, val => val.Raw = "\x00A0");

            value.Raw = AllChars.ToUpper();
            MakeCheck<TextElementValue, ControlСharactersInTextException>(value, constraints, PlainTextValidator.CheckRestrictedSymbols, val => val.Raw = "\r");
        }

        [Fact]
        public void TestFasCommentCheckLength()
        {
            var value = new FasElementValue { Raw = "custom", Text = "text" };
            var constraints = new TextElementConstraints { IsFormatted = false, MaxSymbols = 5 };

            var error = MakeCheck<FasElementValue, ElementTextTooLongException>(value, constraints, PlainTextValidator.CheckLength, val => val.Text = "long text");
            Assert.StrictEqual(constraints.MaxSymbols, error.MaxLength);
            Assert.StrictEqual(value.Text.Length, error.ActualLength);
        }

        [Fact]
        public void TestFasCommentCheckLongWords()
        {
            const int MaxSymbols = 4;
            var value = new FasElementValue { Raw = "custom", Text = new string('a', MaxSymbols) };
            var constraints = new TextElementConstraints { IsFormatted = false, MaxSymbolsPerWord = MaxSymbols };

            var error = MakeCheck<FasElementValue, ElementWordsTooLongException>(value, constraints, PlainTextValidator.CheckWordsLength, val => val.Text = new string('b', MaxSymbols + 1));
            Assert.StrictEqual(MaxSymbols, error.MaxWordLength);
            Assert.StrictEqual(1, error.TooLongWords.Count);
            Assert.StrictEqual(value.Text, error.TooLongWords.First());
        }

        [Fact]
        public void TestFasCommentCheckMaxLines()
        {
            const int MaxLines = 10;
            var value = new FasElementValue { Raw = "custom", Text = new string('\n', MaxLines - 1) };
            var constraints = new TextElementConstraints { IsFormatted = false, MaxLines = MaxLines };

            var error = MakeCheck<FasElementValue, TooManyLinesException>(value, constraints, PlainTextValidator.CheckLinesCount, val => val.Text = new string('\n', MaxLines));
            Assert.StrictEqual(MaxLines, error.MaxLinesCount);
            Assert.StrictEqual(MaxLines + 1, error.ActualLinesCount);
        }

        [Fact]
        public void TestFasCommentCheckRestrictedSymbols()
        {
            const string AllChars = "abcdefghijklmnopqrstuvwxyz \n\t абвгдеёжзийклмнопрстуфхцчшщьыъэюя 1234567890 \\ \" .,;:~'`!? №@#$%^&_ []{}()<> /*-+=";
            var value = new FasElementValue { Raw = "custom", Text = AllChars };
            var constraints = new TextElementConstraints { IsFormatted = false };

            MakeCheck<FasElementValue, NonBreakingSpaceSymbolException>(value, constraints, PlainTextValidator.CheckRestrictedSymbols, val => val.Text = "\x00A0");

            value.Text = AllChars.ToUpper();
            MakeCheck<FasElementValue, ControlСharactersInTextException>(value, constraints, PlainTextValidator.CheckRestrictedSymbols, val => val.Text = "\r");
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
        public void TestTextAllChecks(string rawValue, int? maxLength, int? maxWordLength, int? maxLines, bool containsRestrictedSymbols = true, int expectedErrorsCount = 5)
        {
            var value = new TextElementValue { Raw = rawValue };
            var constraints = new TextElementConstraints { IsFormatted = false, MaxLines = maxLines, MaxSymbols = maxLength, MaxSymbolsPerWord = maxWordLength };

            var errors = new List<Exception>();
            foreach (var validator in AllChecks)
            {
                errors.AddRange(validator(value, constraints));
            }

            Assert.StrictEqual(expectedErrorsCount, errors.Count);
            if (containsRestrictedSymbols)
            {
                Assert.Contains(errors, err => err.GetType() == typeof(NonBreakingSpaceSymbolException));
                Assert.Contains(errors, err => err.GetType() == typeof(ControlСharactersInTextException));
            }

            if (maxLength.HasValue)
            {
                Assert.Contains(errors, err => err.GetType() == typeof(ElementTextTooLongException));
            }

            if (maxLines.HasValue)
            {
                Assert.Contains(errors, err => err.GetType() == typeof(TooManyLinesException));
            }

            if (maxWordLength.HasValue)
            {
                Assert.Contains(errors, err => err.GetType() == typeof(ElementWordsTooLongException));
            }
        }

        private static TException MakeCheck<TValue, TException>(TValue value, IElementConstraints constraints, Validator validator, Action<TValue> valueChanger)
            where TValue : IObjectElementValue
            where TException : Exception
        {
            Assert.Empty(validator(value, constraints));
            valueChanger(value);

            var errors = validator(value, constraints).ToList();
            Assert.StrictEqual(1, errors.Count);
            Assert.IsType<TException>(errors.First());

            return (TException)errors.First();
        }
    }
}

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
        [Fact]
        public void TestTextCheckLength()
        {
            const int MaxSymbols = 50;
            var value = new TextElementValue { Raw = new string('a', MaxSymbols) };
            var constraints = new TextElementConstraints { IsFormatted = false, MaxSymbols = MaxSymbols };

            Assert.Empty(PlainTextValidator.CheckLength(value, constraints));

            value.Raw = new string('b', MaxSymbols + 1);
            var errors = PlainTextValidator.CheckLength(value, constraints).ToList();
            Assert.StrictEqual(1, errors.Count);
            Assert.IsType<ElementTextTooLongException>(errors.First());

            var error = (ElementTextTooLongException)errors.First();
            Assert.StrictEqual(MaxSymbols, error.MaxLength);
            Assert.StrictEqual(value.Raw.Length, error.ActualLength);
        }

        [Fact]
        public void TestTextCheckLongWords()
        {
            const int MaxSymbols = 10;
            var value = new TextElementValue { Raw = new string('a', MaxSymbols) };
            var constraints = new TextElementConstraints { IsFormatted = false, MaxSymbolsPerWord = MaxSymbols };

            Assert.Empty(PlainTextValidator.CheckWordsLength(value, constraints));

            value.Raw = new string('b', MaxSymbols + 1);
            var errors = PlainTextValidator.CheckWordsLength(value, constraints).ToList();
            Assert.StrictEqual(1, errors.Count);
            Assert.IsType<ElementWordsTooLongException>(errors.First());

            var error = (ElementWordsTooLongException)errors.First();
            Assert.StrictEqual(MaxSymbols, error.MaxWordLength);
            Assert.StrictEqual(1, error.TooLongWords.Count);
            Assert.StrictEqual(value.Raw, error.TooLongWords.First());
        }

        [Fact]
        public void TestFasCommentCheckLength()
        {
            var value = new FasElementValue { Raw = "custom", Text = "text" };
            var constraints = new TextElementConstraints { IsFormatted = false, MaxSymbols = 5 };

            Assert.Empty(PlainTextValidator.CheckLength(value, constraints));

            value.Text = "long text";
            var errors = PlainTextValidator.CheckLength(value, constraints).ToList();
            Assert.StrictEqual(1, errors.Count);
            Assert.IsType<ElementTextTooLongException>(errors.First());

            var error = (ElementTextTooLongException)errors.First();
            Assert.StrictEqual(constraints.MaxSymbols, error.MaxLength);
            Assert.StrictEqual(value.Text.Length, error.ActualLength);
        }

        [Fact]
        public void TestFasCommentCheckLongWords()
        {
            const int MaxSymbols = 4;
            var value = new FasElementValue { Raw = "custom", Text = new string('a', MaxSymbols) };
            var constraints = new TextElementConstraints { IsFormatted = false, MaxSymbolsPerWord = MaxSymbols };

            Assert.Empty(PlainTextValidator.CheckWordsLength(value, constraints));

            value.Text = new string('b', MaxSymbols + 1);
            var errors = PlainTextValidator.CheckWordsLength(value, constraints).ToList();
            Assert.StrictEqual(1, errors.Count);
            Assert.IsType<ElementWordsTooLongException>(errors.First());

            var error = (ElementWordsTooLongException)errors.First();
            Assert.StrictEqual(MaxSymbols, error.MaxWordLength);
            Assert.StrictEqual(1, error.TooLongWords.Count);
            Assert.StrictEqual(value.Text, error.TooLongWords.First());
        }
    }
}

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
            var constraints = new TextElementConstraints { IsFormatted = false, MaxLines = 1, MaxSymbols = MaxSymbols, MaxSymbolsPerWord = MaxSymbols };

            Assert.Empty(PlainTextValidator.CheckLength(value, constraints));
            Assert.Empty(PlainTextValidator.CheckWordsLength(value, constraints));
            Assert.Empty(PlainTextValidator.CheckLinesCount(value, constraints));
            Assert.Empty(PlainTextValidator.CheckRestrictedSymbols(value, constraints));

            value.Raw = new string('b', MaxSymbols + 1);
            var errors = PlainTextValidator.CheckLength(value, constraints).ToList();
            Assert.StrictEqual(1, errors.Count);
            Assert.IsType<ElementTextTooLongException>(errors.First());

            errors = PlainTextValidator.CheckWordsLength(value, constraints).ToList();
            Assert.StrictEqual(1, errors.Count);
            Assert.IsType<ElementWordsTooLongException>(errors.First());
        }

        [Fact]
        public void TestFasCommentCheckLength()
        {
            var value = new FasElementValue { Raw = "custom", Text = "text" };
            var constraints = new TextElementConstraints { IsFormatted = false, MaxLines = 1, MaxSymbols = 5, MaxSymbolsPerWord = 5 };

            Assert.Empty(PlainTextValidator.CheckLength(value, constraints));
            Assert.Empty(PlainTextValidator.CheckWordsLength(value, constraints));
            Assert.Empty(PlainTextValidator.CheckLinesCount(value, constraints));
            Assert.Empty(PlainTextValidator.CheckRestrictedSymbols(value, constraints));

            value.Text = "long text";
            var errors = PlainTextValidator.CheckLength(value, constraints).ToList();
            Assert.StrictEqual(1, errors.Count);
            Assert.IsType<ElementTextTooLongException>(errors.First());
        }
    }
}

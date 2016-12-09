using System.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.Exceptions;

// ReSharper disable UnusedMember.Global
namespace VStore.UnitTests
{
    [TestClass]
    public class PlainTextValidatorTests
    {
        [TestMethod]
        public void TestTextCheckLength()
        {
            const int MaxSymbols = 50;
            var value = new TextElementValue { Raw = new string('a', MaxSymbols) };
            var constraints = new TextElementConstraints { IsFormatted = false, MaxLines = 1, MaxSymbols = MaxSymbols, MaxSymbolsPerWord = MaxSymbols };

            var errors = PlainTextValidator.CheckLength(value, constraints).ToList();
            Assert.IsFalse(errors.Any(), "There shouldn't be any validation errors");

            value.Raw = new string('b', MaxSymbols + 1);
            errors = PlainTextValidator.CheckLength(value, constraints).ToList();
            Assert.AreEqual(2, errors.Count, "There should be exactly two validation errors");
            Assert.IsNotNull(errors.SingleOrDefault(e => e is ElementTextTooLongException), "One validation error must be an ElementTextTooLongException");
            Assert.IsNotNull(errors.SingleOrDefault(e => e is ElementWordsTooLongException), "One validation error must be an ElementWordsTooLongException");
        }

        [TestMethod]
        public void TestFasCommentCheckLength()
        {
            var value = new FasElementValue { Raw = "custom", Text = "text" };
            var constraints = new TextElementConstraints { IsFormatted = false, MaxLines = 1, MaxSymbols = 5, MaxSymbolsPerWord = 5 };

            var errors = PlainTextValidator.CheckLength(value, constraints).ToList();
            Assert.AreEqual(0, errors.Count, "There shouldn't be any validation errors");

            value.Text = "long text";
            errors = PlainTextValidator.CheckLength(value, constraints).ToList();
            Assert.AreEqual(1, errors.Count, "There should be exactly one validation error");
            Assert.IsInstanceOfType(errors.First(), typeof(ElementTextTooLongException), "Validation error must be an ElementTextTooLongException");
        }
    }
}

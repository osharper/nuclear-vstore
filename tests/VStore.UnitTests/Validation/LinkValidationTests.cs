using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.Errors;

using Xunit;

// ReSharper disable UnusedMember.Global
namespace VStore.UnitTests.Validation
{
    public class LinkValidationTests
    {
        [Fact]
        public void TestHttpLinkValidation()
        {
            var value = new TextElementValue { Raw = "http://google.com/search?abc=123" };
            var constraints = new LinkElementConstraints();

            var error = TestHelpers.MakeValidationCheck<TextElementValue, IncorrectLinkError>(
                value,
                constraints,
                LinkValidator.CheckLink,
                val => val.Raw = "http://abc. com");
            Assert.Equal(ElementConstraintViolations.ValidLink, error.ErrorType);
        }

        [Fact]
        public void TestHttpsLinkValidation()
        {
            var value = new TextElementValue { Raw = "https://дубль-гис.рф" };
            var constraints = new LinkElementConstraints();

            var error = TestHelpers.MakeValidationCheck<TextElementValue, IncorrectLinkError>(
                value,
                constraints,
                LinkValidator.CheckLink,
                val => val.Raw = "https://abc. com");
            Assert.Equal(ElementConstraintViolations.ValidLink, error.ErrorType);
        }

        [Fact]
        public void TestIncorrectLinkValidation()
        {
            var value = new TextElementValue { Raw = "http://дубль-гис.рф" };
            var constraints = new LinkElementConstraints { MaxSymbols = value.Raw.Length };

            var error = TestHelpers.MakeValidationCheck<TextElementValue, IncorrectLinkError>(
                value,
                constraints,
                LinkValidator.CheckLink,
                val => val.Raw = "http://дубль-гис.\tрф");
            Assert.Equal(ElementConstraintViolations.ValidLink, error.ErrorType);
        }

        [Fact]
        public void TestLinkWithRtlAndLtrValidation()
        {
            var value = new TextElementValue { Raw = "http://дубль-гис.рф" };
            var constraints = new LinkElementConstraints { MaxSymbols = value.Raw.Length };

            var error = TestHelpers.MakeValidationCheck<TextElementValue, IncorrectLinkError>(
                value,
                constraints,
                LinkValidator.CheckLink,
                val => val.Raw = "\u200fhttp://дубль-гис.рф\u200e");
            Assert.Equal(ElementConstraintViolations.ValidLink, error.ErrorType);
        }

        [Fact]
        public void TestLinkSchemeValidation()
        {
            var value = new TextElementValue { Raw = "http://дубль-гис.рф" };
            var constraints = new LinkElementConstraints();

            var error = TestHelpers.MakeValidationCheck<TextElementValue, IncorrectLinkError>(
                value,
                constraints,
                LinkValidator.CheckLink,
                val => val.Raw = "ftp://дубль-гис.рф");
            Assert.Equal(ElementConstraintViolations.ValidLink, error.ErrorType);

            value.Raw = "http://xn----9sbhbxp9bk7f.xn--p1ai";
            error = TestHelpers.MakeValidationCheck<TextElementValue, IncorrectLinkError>(
                value,
                constraints,
                LinkValidator.CheckLink,
                val => val.Raw = "file://дубль-гис.рф");
            Assert.Equal(ElementConstraintViolations.ValidLink, error.ErrorType);
        }

        [Fact]
        public void TestLinkLengthValidation()
        {
            var value = new TextElementValue { Raw = "http://дубль-гис.рф" };
            var constraints = new LinkElementConstraints { MaxSymbols = value.Raw.Length };

            var error = TestHelpers.MakeValidationCheck<TextElementValue, ElementTextTooLongError>(
                value,
                constraints,
                PlainTextValidator.CheckLength,
                val => val.Raw += "/");
            Assert.Equal(ElementConstraintViolations.MaxSymbols, error.ErrorType);
        }

        [Fact]
        public void TestLinkRestrictedSymbolsValidation()
        {
            var value = new TextElementValue { Raw = "http://дубль-гис.рф" };
            var constraints = new LinkElementConstraints();

            var controlCharsError = TestHelpers.MakeValidationCheck<TextElementValue, ControlCharactersInTextError>(
                value,
                constraints,
                PlainTextValidator.CheckRestrictedSymbols,
                val => val.Raw += "\r");
            Assert.Equal(ElementConstraintViolations.WithoutControlChars, controlCharsError.ErrorType);

            value.Raw = "http://google.com/search?abc=123";
            var nonBreakingSpaceError = TestHelpers.MakeValidationCheck<TextElementValue, NonBreakingSpaceSymbolError>(
                value,
                constraints,
                PlainTextValidator.CheckRestrictedSymbols,
                val => val.Raw += (char)160);
            Assert.Equal(ElementConstraintViolations.WithoutNonBreakingSpace, nonBreakingSpaceError.ErrorType);
        }
    }
}

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.Errors;

using Xunit;

// ReSharper disable UnusedMember.Global
namespace VStore.UnitTests
{
    public class LinkValidationTests
    {
        [Fact]
        public void TestHttpLinkValidation()
        {
            var value = new TextElementValue { Raw = "http://дубль-гис.рф" };

            var error = TestHelpers.MakeCheck<TextElementValue, IncorrectLinkError>(
                value,
                null,
                LinkValidator.CheckLink,
                val => val.Raw = "http://abc. com");
            Assert.StrictEqual(ElementValidationErrors.IncorrectLink, error.ErrorType);
        }

        [Fact]
        public void TestHttpsLinkValidation()
        {
            var value = new TextElementValue { Raw = "https://дубль-гис.рф" };

            var error = TestHelpers.MakeCheck<TextElementValue, IncorrectLinkError>(
                value,
                null,
                LinkValidator.CheckLink,
                val => val.Raw = "https://abc. com");
            Assert.StrictEqual(ElementValidationErrors.IncorrectLink, error.ErrorType);
        }

        [Fact]
        public void TestLinkSchemeValidation()
        {
            var value = new TextElementValue { Raw = "http://дубль-гис.рф" };

            var error = TestHelpers.MakeCheck<TextElementValue, IncorrectLinkError>(
                value,
                null,
                LinkValidator.CheckLink,
                val => val.Raw = "ftp://дубль-гис.рф");
            Assert.StrictEqual(ElementValidationErrors.IncorrectLink, error.ErrorType);

            value.Raw = "http://xn----9sbhbxp9bk7f.xn--p1ai";
            TestHelpers.MakeCheck<TextElementValue, IncorrectLinkError>(
                value,
                null,
                LinkValidator.CheckLink,
                val => val.Raw = "file://дубль-гис.рф");
        }
    }
}

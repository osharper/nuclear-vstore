using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.Errors;

using NUnit.Framework;

// ReSharper disable UnusedMember.Global
namespace VStore.UnitTests
{
    [TestFixture]
    public class LinkValidationTests
    {
        [Test]
        public void TestHttpLinkValidation()
        {
            var value = new TextElementValue { Raw = "http://дубль-гис.рф" };

            var error = TestHelpers.MakeCheck<TextElementValue, IncorrectLinkError>(
                value,
                null,
                LinkValidator.CheckLink,
                val => val.Raw = "http://abc. com");
            Assert.AreEqual(ElementValidationErrors.IncorrectLink, error.ErrorType);
        }

        [Test]
        public void TestHttpsLinkValidation()
        {
            var value = new TextElementValue { Raw = "https://дубль-гис.рф" };

            var error = TestHelpers.MakeCheck<TextElementValue, IncorrectLinkError>(
                value,
                null,
                LinkValidator.CheckLink,
                val => val.Raw = "https://abc. com");
            Assert.AreEqual(ElementValidationErrors.IncorrectLink, error.ErrorType);
        }

        [Test]
        public void TestLinkSchemeValidation()
        {
            var value = new TextElementValue { Raw = "http://дубль-гис.рф" };

            var error = TestHelpers.MakeCheck<TextElementValue, IncorrectLinkError>(
                value,
                null,
                LinkValidator.CheckLink,
                val => val.Raw = "ftp://дубль-гис.рф");
            Assert.AreEqual(ElementValidationErrors.IncorrectLink, error.ErrorType);

            value.Raw = "http://xn----9sbhbxp9bk7f.xn--p1ai";
            TestHelpers.MakeCheck<TextElementValue, IncorrectLinkError>(
                value,
                null,
                LinkValidator.CheckLink,
                val => val.Raw = "file://дубль-гис.рф");
        }
    }
}

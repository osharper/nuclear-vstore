using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation;

using Xunit;

namespace VStore.UnitTests.Validation
{
    public sealed class ColorValidationTests
    {
        [Theory]
        [InlineData("#aabbcc")]
        [InlineData("#Fa1bc1")]
        [InlineData("#000010")]
        [InlineData("#AAacAe")]
        [InlineData("#AaAaAa")]
        [InlineData("#1488FF")]
        public void TestValidColor(string color)
        {
            var value = new ColorElementValue {Raw = color};
            var constraints = new ColorElementConstraints();

            var errors = ColorValidator.CheckValidColor(value, constraints);

            Assert.Empty(errors);
        }

        [Theory]
        [InlineData("aabbcc")]
        [InlineData("aabbcc1")]
        [InlineData("##aabbcc")]
        [InlineData("##aabbc")]
        [InlineData("#000000a")]
        [InlineData("#AABBGG")]
        [InlineData("123123#")]
        [InlineData("AAA#BBB")]
        public void TestInvalidColor(string color)
        {
            var value = new ColorElementValue {Raw = color};
            var constraints = new ColorElementConstraints();

            var errors = ColorValidator.CheckValidColor(value, constraints);

            Assert.NotEmpty(errors);
        }
    }
}
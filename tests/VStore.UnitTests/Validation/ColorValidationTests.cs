using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation;

using Xunit;

namespace VStore.UnitTests.Validation
{
    public sealed class ColorValidationTests
    {
        [Theory]
        [InlineData("#AABBCC")]
        [InlineData("#000010")]
        [InlineData("#1488FF")]
        [InlineData("#ABCDEF")]
        [InlineData("#123456")]
        [InlineData("#987654")]
        [InlineData("")]
        [InlineData(null)]
        public void TestValidColor(string color)
        {
            var value = new ColorElementValue {Raw = color};
            var constraints = new ColorElementConstraints();

            var errors = ColorValidator.CheckValidColor(value, constraints);

            Assert.Empty(errors);
        }

        [Theory]
        [InlineData("AABBCC")]
        [InlineData("#AABBcC")]
        [InlineData("##AABBC")]
        [InlineData("#000000A")]
        [InlineData("#AABBGG")]
        [InlineData("123123#")]
        [InlineData("AAA#BBB")]
        [InlineData("#ABCDEБ")]
        [InlineData("#ABCDE")]
        [InlineData("#ABCDEF ")]
        [InlineData(" #ABCDEF")]
        [InlineData("\t#ABCDEF")]
        [InlineData(" ")]
        public void TestInvalidColor(string color)
        {
            var value = new ColorElementValue {Raw = color};
            var constraints = new ColorElementConstraints();

            var errors = ColorValidator.CheckValidColor(value, constraints);

            Assert.NotEmpty(errors);
        }
    }
}
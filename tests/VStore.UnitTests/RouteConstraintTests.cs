using NuClear.VStore.Host.Routing;

using Xunit;

namespace VStore.UnitTests
{
    public class RouteConstraintTests
    {
        [Theory]
        [InlineData("ru", true)]
        [InlineData("RU", true)]
        [InlineData("rU", true)]
        [InlineData("Ru", true)]
        [InlineData("En", true)]
        [InlineData("EN", true)]
        [InlineData("en", true)]
        [InlineData("unspecified", true)]
        [InlineData("unspecified2", false)]
        [InlineData("enn", false)]
        [InlineData("ru-en", false)]
        [InlineData("ru_en", false)]
        [InlineData("ru/en", false)]
        [InlineData("en2", false)]
        [InlineData("2en", false)]
        [InlineData("-1", false)]
        [InlineData("0", false)]
        [InlineData("99", false)]
        public void TestLanguageRouteConstraint(string parameterValue, bool expected)
        {
            var constraint = new LanguageRouteConstraint();
            var actual = TestHelpers.TestRouteConstraint(constraint, parameterValue);
            Assert.Equal(expected, actual);
        }
    }
}

using NuClear.VStore.Objects.ContentPreprocessing;

using Xunit;

namespace VStore.UnitTests.Preprocessing
{
    public class LinkPreprocessingTests
    {
        [Fact]
        public void TestOnNullString()
        {
            Assert.Equal(null, ElementTextHarmonizer.ProcessLink(null));
        }

        [Fact]
        public void TestOnEmptyString()
        {
            Assert.Equal(string.Empty, ElementTextHarmonizer.ProcessLink(string.Empty));
        }

        [Fact]
        public void TestOnLeadingAndTrailingWhitespaceSymbols()
        {
            Assert.Equal("Случайный \n пробел", ElementTextHarmonizer.ProcessLink(" \t \n  Случайный \n пробел \t \n \r  "));
        }
    }
}

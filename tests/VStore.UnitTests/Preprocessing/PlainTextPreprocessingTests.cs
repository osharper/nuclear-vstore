using NuClear.VStore.Objects.ContentPreprocessing;

using Xunit;

namespace VStore.UnitTests.Preprocessing
{
    public class PlainTextPreprocessingTests
    {
        [Fact]
        public void TestOnNullString()
        {
            Assert.Null(ElementTextHarmonizer.ProcessPlain(null));
        }

        [Fact]
        public void TestOnEmptyString()
        {
            Assert.Equal(string.Empty, ElementTextHarmonizer.ProcessPlain(string.Empty));
        }

        [Fact]
        public void TestOnLineBreaks()
        {
            Assert.Equal("\t\nСлучайный \r\n\tтекст", ElementTextHarmonizer.ProcessPlain("\t\nСлучайный \r\r\n\tтекст"));
        }

        [Fact]
        public void TestOnNonbreakingSpace()
        {
            Assert.Equal("&nbsp; пробел", ElementTextHarmonizer.ProcessPlain("&nbsp;" + ((char)160).ToString() + "пробел"));
        }

        [Fact]
        public void TestOnWhitespaceSymbolsInTheEnd()
        {
            Assert.Equal("\t\nСлучайный пробел", ElementTextHarmonizer.ProcessPlain("\t\nСлучайный пробел\t\n \r"));
        }

        [Fact]
        public void TestOnAllConditions()
        {
            Assert.Equal("\t\nСлучайный пробел", ElementTextHarmonizer.ProcessPlain("\t\r\nСлучайный" + ((char)160).ToString() + "пробел\t\n \r"));
        }
    }
}

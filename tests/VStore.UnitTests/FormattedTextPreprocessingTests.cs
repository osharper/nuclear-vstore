using NuClear.VStore.Objects.ContentPreprocessing;

using Xunit;

namespace VStore.UnitTests
{
    public class FormattedTextPreprocessingTests
    {
        [Fact]
        public void TestOnEmptyListItems()
        {
            Assert.Equal("<ul></ul>", ElementTextHarmonizer.ProcessFormatted("<ul><li></li><li></li></ul>"));
        }

        [Fact]
        public void TestOnEmptyListItemsAmongNonEmptyOnes()
        {
            Assert.Equal("<ul><li>1</li><li>2</li></ul>", ElementTextHarmonizer.ProcessFormatted("<ul><li>1</li><li></li><li>2</li></ul>"));
        }

        [Fact]
        public void TestOnEmptyListItemsWithSpaces()
        {
            Assert.Equal("<ul></ul>", ElementTextHarmonizer.ProcessFormatted("<ul><li> </li><li>                  </li></ul>"));
        }

        [Fact]
        public void TestOnEmptyListItemsWithTabs()
        {
            Assert.Equal("<ul></ul>", ElementTextHarmonizer.ProcessFormatted("<ul><li>   </li><li>\t\t</li></ul>"));
        }

        [Fact]
        public void TestOnEmptyListItemsWithBreaks()
        {
            Assert.Equal("<ul></ul>", ElementTextHarmonizer.ProcessFormatted("<ul><li><br></li><li><br /></li></ul>"));
        }

        [Fact]
        public void TestOnEmptyListItemsWithNonbreakingSpace()
        {
            Assert.Equal("<ul></ul>", ElementTextHarmonizer.ProcessFormatted("<ul><li>&nbsp;</li><li>" + ((char)160).ToString() + "</li></ul>"));
        }

        [Fact]
        public void TestOnEmptyListItemsWithHtmlComment()
        {
            Assert.Equal("<ul></ul>", ElementTextHarmonizer.ProcessFormatted("<ul><li><!-- This is comment --></li><li></li></ul>"));
        }

        [Fact]
        public void TestOnEmptyListItemsWithLineBreaks()
        {
            Assert.Equal("<ul></ul>", ElementTextHarmonizer.ProcessFormatted("<ul><li>\n</li><li>\r\n</li></ul>"));
        }

        [Fact]
        public void TestOnEmptyListItemsWithGroupSeparator()
        {
            Assert.Equal("<ul></ul>", ElementTextHarmonizer.ProcessFormatted("<ul><li>\u001d</li></ul>"));
        }

        [Fact]
        public void TestOnAllConditions()
        {
            Assert.Equal("<b> </b>" + ((char)160).ToString() + "<ul>   </ul>",
                ElementTextHarmonizer.ProcessFormatted("<b>&nbsp;</b>\r\n\n" + ((char)160).ToString() + "<ul> <li>\u001d<!-- comment --></li> <li>&nbsp;</li> </ul>\n\r\n \t"));
        }
    }
}

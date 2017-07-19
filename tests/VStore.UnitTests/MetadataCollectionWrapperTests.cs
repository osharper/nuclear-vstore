using Amazon.S3.Model;
using NuClear.VStore.S3;
using System;
using System.Globalization;
using System.Text.RegularExpressions;
using Xunit;

namespace VStore.UnitTests
{
    public class MetadataCollectionWrapperTests
    {
        [Fact]
        public void ShouldWrapUsingInvariantCulture()
        {
            var wrapper = MetadataCollectionWrapper.For(new MetadataCollection());
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var value = DateTime.UtcNow;
            wrapper.Write(MetadataElement.ExpiresAt, value);
            CultureInfo.CurrentCulture = new CultureInfo("ru-RU");
            var res = wrapper.Read<DateTime>(MetadataElement.ExpiresAt);
            Assert.Equal(value, res);
        }

        [Fact]
        public void ShouldWrapFilename()
        {
            var wrapper = MetadataCollectionWrapper.For(new MetadataCollection());
            var value = "my_file.png";
            wrapper.WriteEncoded(MetadataElement.Filename, value);
            var res = wrapper.ReadEncoded<string>(MetadataElement.Filename);
            Assert.Equal(value, res);
        }

        [Fact]
        public void ShouldWorkForNonUsAsciiSymbols()
        {
            var wrapper = MetadataCollectionWrapper.For(new MetadataCollection());
            var value = "Вася Пупкин";
            wrapper.WriteEncoded(MetadataElement.AuthorName, value);
            var res = wrapper.ReadEncoded<string>(MetadataElement.AuthorName);
            Assert.Equal(value, res);
        }

        [Fact]
        public void ShouldWorkForAlreadyEncodedValue()
        {
            var wrapper = MetadataCollectionWrapper.For(new MetadataCollection());
            var value = "Вася Пупкин";
            var encodedValue = Uri.EscapeDataString(value);
            wrapper.WriteEncoded(MetadataElement.AuthorName, "utf-8''" + encodedValue);
            var res = wrapper.ReadEncoded<string>(MetadataElement.AuthorName);
            Assert.Equal(value, res);
        }
    }
}

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
        public void ShouldWrapUri()
        {
            var wrapper = MetadataCollectionWrapper.For(new MetadataCollection());
            var value = new Uri("http://dot.net");
            wrapper.Write(MetadataElement.PreviewUrl, value);
            var res = wrapper.Read<Uri>(MetadataElement.PreviewUrl);
            Assert.Equal(value, res);
        }

        [Fact]
        public void ShouldWrapFilenameAsBase64()
        {
            var wrapper = MetadataCollectionWrapper.For(new MetadataCollection());
            var value = "my_file.png";
            wrapper.Write(MetadataElement.Filename, value);
            var res = wrapper.Read<string>(MetadataElement.Filename);
            Assert.Equal(value, res);

            var unwrapped = wrapper.Unwrap();
            var keys = new string[unwrapped.Keys.Count];
            unwrapped.Keys.CopyTo(keys, 0);
            Assert.True(IsBase64String(unwrapped[keys[0]]));
        }

        private bool IsBase64String(string s)
        {
            return (s.Length % 4 == 0) && Regex.IsMatch(s, @"^[a-zA-Z0-9\+/]*={0,3}$", RegexOptions.None);
        }
    }
}

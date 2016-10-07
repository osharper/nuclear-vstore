using System;
using System.Text;

using Amazon.S3.Model;

namespace NuClear.VStore.s3
{
    public sealed class MetadataCollectionWrapper
    {
        private readonly MetadataCollection _metadataCollection;
        private readonly Encoding _defaultEncoding;

        private MetadataCollectionWrapper(MetadataCollection metadataCollection, Encoding defaultEncoding)
        {
            _metadataCollection = metadataCollection;
            _defaultEncoding = defaultEncoding;
        }

        public static MetadataCollectionWrapper For(MetadataCollection metadataCollection)
        {
            return new MetadataCollectionWrapper(metadataCollection, Encoding.UTF8);
        }

        public T Read<T>(MetadataElement metadataElement)
        {
            var name = AsMetadata(metadataElement);
            var value = _metadataCollection[name];
            if (typeof(T) == typeof(string))
            {
                value = _defaultEncoding.GetString(Convert.FromBase64String(value));
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }

        public void Write<T>(MetadataElement metadataElement, T value)
        {
            var name = AsMetadata(metadataElement);
            var valueToWrite = value.ToString();
            if (typeof(T) == typeof(string))
            {
                valueToWrite = Convert.ToBase64String(_defaultEncoding.GetBytes(valueToWrite));
            }

            _metadataCollection[name] = valueToWrite;
        }

        private static string AsMetadata(MetadataElement metadataElement) => $"x-amz-meta-{metadataElement.ToString().ToLower()}";
    }
}
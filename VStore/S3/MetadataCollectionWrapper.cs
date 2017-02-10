using System;
using System.Text;

using Amazon.S3.Model;

namespace NuClear.VStore.S3
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
            var name = AsMetadataKey(metadataElement);
            var value = _metadataCollection[name];
            if (value == null)
            {
                return default(T);
            }

            if (metadataElement == MetadataElement.Filename)
            {
                value = _defaultEncoding.GetString(Convert.FromBase64String(value));
            }

            return (T)Convert.ChangeType(value, typeof(T));
        }

        public void Write<T>(MetadataElement metadataElement, T value)
        {
            var name = AsMetadataKey(metadataElement);
            var valueToWrite = value.ToString();
            if (metadataElement == MetadataElement.Filename)
            {
                valueToWrite = Convert.ToBase64String(_defaultEncoding.GetBytes(valueToWrite));
            }

            _metadataCollection[name] = valueToWrite;
        }

        public MetadataCollection Unwrap() => _metadataCollection;

        private static string AsMetadataKey(MetadataElement metadataElement) => $"x-amz-meta-{metadataElement.ToString().ToLower()}";
    }
}
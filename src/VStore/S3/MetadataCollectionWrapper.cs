using System;

using Amazon.S3.Model;
using Newtonsoft.Json;

namespace NuClear.VStore.S3
{
    public sealed class MetadataCollectionWrapper
    {
        private const string Utf8Token = "utf-8''";

        private readonly MetadataCollection _metadataCollection;

        private MetadataCollectionWrapper(MetadataCollection metadataCollection)
        {
            _metadataCollection = metadataCollection;
        }

        public static MetadataCollectionWrapper For(MetadataCollection metadataCollection)
        {
            return new MetadataCollectionWrapper(metadataCollection);
        }

        public T Read<T>(MetadataElement metadataElement)
        {
            var name = AsMetadataKey(metadataElement);
            var value = _metadataCollection[name];
            if (value == null)
            {
                return default(T);
            }

            if (value.StartsWith(Utf8Token, StringComparison.OrdinalIgnoreCase))
            {
                value = Uri.UnescapeDataString(value.Substring(Utf8Token.Length));
            }

            if (typeof(T) == typeof(string))
            {
                return (T)(object)value;
            }

            return JsonConvert.DeserializeObject<T>(value);
        }

        public void Write<T>(MetadataElement metadataElement, T value)
        {
            var stringValue = value as string;
            var valueToWrite = stringValue ?? JsonConvert.SerializeObject(value);
            if (!valueToWrite.StartsWith(Utf8Token, StringComparison.OrdinalIgnoreCase))
            {
                valueToWrite = Utf8Token + Uri.EscapeDataString(valueToWrite);
            }

            var name = AsMetadataKey(metadataElement);
            _metadataCollection[name] = valueToWrite;
        }

        private static string AsMetadataKey(MetadataElement metadataElement) => $"x-amz-meta-{metadataElement.ToString().ToLower()}";
    }
}
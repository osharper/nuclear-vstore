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

        public T ReadEncoded<T>(MetadataElement metadataElement) => Read<T>(metadataElement, true);

        public T Read<T>(MetadataElement metadataElement) => Read<T>(metadataElement, false);

        public void WriteEncoded<T>(MetadataElement metadataElement, T value) => Write(metadataElement, value, true);

        public void Write<T>(MetadataElement metadataElement, T value) => Write(metadataElement, value, false);

        private static string AsMetadataKey(MetadataElement metadataElement, bool encoded)
        {
            var headerName = $"x-amz-meta-{metadataElement.ToString().ToLower()}";
            if (encoded)
            {
                headerName += "*";
            }

            return headerName;
        }

        private T Read<T>(MetadataElement metadataElement, bool decode)
        {
            var name = AsMetadataKey(metadataElement, decode);
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

        private void Write<T>(MetadataElement metadataElement, T value, bool encode)
        {
            var stringValue = value as string;
            var valueToWrite = stringValue ?? JsonConvert.SerializeObject(value);
            if (encode && !valueToWrite.StartsWith(Utf8Token, StringComparison.OrdinalIgnoreCase))
            {
                valueToWrite = Utf8Token + Uri.EscapeDataString(valueToWrite);
            }

            var name = AsMetadataKey(metadataElement, encode);
            _metadataCollection[name] = valueToWrite;
        }
    }
}
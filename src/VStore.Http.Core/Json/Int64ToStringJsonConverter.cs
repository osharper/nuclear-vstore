using System;

using Newtonsoft.Json;

namespace NuClear.VStore.Http.Core.Json
{
    public sealed class Int64ToStringJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(long);

        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToString());
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }
    }
}
using System;
using System.Reflection;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Json
{
    public sealed class ObjectElementDescriptorJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => typeof(IObjectElementDescriptor).IsAssignableFrom(objectType);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var objectElementDescriptor = (IObjectElementDescriptor)value;
            var elementDescriptor = new ElementDescriptor(
                                        objectElementDescriptor.Type,
                                        objectElementDescriptor.TemplateCode,
                                        objectElementDescriptor.Properties,
                                        objectElementDescriptor.Constraints);

            var json = JObject.FromObject(elementDescriptor, serializer);
            json[Tokens.ValueToken] = JToken.FromObject(objectElementDescriptor.Value, serializer);

            json.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var json = JObject.Load(reader);

            var valueToken = json[Tokens.ValueToken];
            var elementDescriptor = json.ToObject<IElementDescriptor>(serializer);

            var value = valueToken.AsObjectElementValue(elementDescriptor.Type);

            return new ObjectElementDescriptor(elementDescriptor, value);
        }
    }
}
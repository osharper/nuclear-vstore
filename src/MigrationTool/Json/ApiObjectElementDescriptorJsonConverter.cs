using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;

namespace MigrationTool.Json
{
    internal class ApiObjectElementDescriptorJsonConverter : JsonConverter
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
            json[Tokens.IdToken] = objectElementDescriptor.Id;

            json.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var json = JObject.Load(reader);

            var valueToken = json[Tokens.ValueToken];
            var elementDescriptor = json.ToObject<IElementDescriptor>(serializer);

            var value = valueToken.AsObjectElementValue(elementDescriptor.Type);

            return new ApiObjectElementDescriptor
            {
                Id = json.Value<long>(Tokens.IdToken),
                UploadUrl = json.Value<string>("uploadUrl"),
                Value = value,
                TemplateCode = elementDescriptor.TemplateCode,
                Constraints = elementDescriptor.Constraints,
                Properties = elementDescriptor.Properties,
                Type = elementDescriptor.Type
            };
        }
    }
}
using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Json;

namespace CloningTool.Json
{
    internal class ApiObjectDescriptorJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => typeof(ObjectDescriptor).IsAssignableFrom(objectType);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var objectDescriptor = (ObjectDescriptor)value;
            var json = new JObject
                {
                    { Tokens.IdToken, objectDescriptor.Id },
                    { Tokens.TemplateIdToken, objectDescriptor.TemplateId },
                    { Tokens.TemplateVersionIdToken, objectDescriptor.TemplateVersionId },
                    { Tokens.LanguageToken, objectDescriptor.Language.ToString().ToLowerInvariant() },
                    { Tokens.PropertiesToken, objectDescriptor.Properties },
                    { Tokens.ElementsToken, JArray.FromObject(objectDescriptor.Elements, serializer) }
                };
            json.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}

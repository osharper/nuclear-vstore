using System;
using System.Collections.Generic;
using System.Reflection;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Json
{
    public sealed class ObjectDescriptorJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => typeof(IObjectDescriptor).IsAssignableFrom(objectType);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var objectDescriptor = (IObjectDescriptor)value;
            var json = new JObject
                           {
                               [Tokens.PropertiesToken] = objectDescriptor.Properties,
                               [Tokens.ElementsToken] = JArray.FromObject(objectDescriptor.Elements, serializer)
                           };
            json.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var descriptors = obj[Tokens.ElementsToken];
            var elementDescriptors = DeserializeObjectElementDescriptors(descriptors, serializer);

            obj.Remove(Tokens.ElementsToken);

            var objectDescriptor = obj.ToObject<ObjectDescriptor>();
            objectDescriptor.Elements = elementDescriptors;

            return objectDescriptor;
        }

        private static IReadOnlyCollection<IObjectElementDescriptor> DeserializeObjectElementDescriptors(JToken token, JsonSerializer serializer)
        {
            var elementDescriptors = new List<IObjectElementDescriptor>();
            foreach (var descriptor in token)
            {
                var type = descriptor[Tokens.TypeToken].ToString();
                var descriptorType = (ElementDescriptorType)Enum.Parse(typeof(ElementDescriptorType), type, true);

                var elementDescriptor = descriptor.ToObject<IElementDescriptor>(serializer);
                if (elementDescriptor == null)
                {
                    return Array.Empty<IObjectElementDescriptor>();
                }

                var id = descriptor[Tokens.IdToken].ToObject<long>();

                var versionId = string.Empty;
                var versionIdToken = descriptor.SelectToken(Tokens.VersionIdToken);
                if (versionIdToken != null)
                {
                    versionId = versionIdToken.ToObject<string>();
                }

                var value = descriptor[Tokens.ValueToken].AsObjectElementValue(descriptorType);
                if (value == null)
                {
                    return Array.Empty<IObjectElementDescriptor>();
                }

                elementDescriptors.Add(new ObjectElementDescriptor(elementDescriptor, value) { Id = id, VersionId = versionId });
            }

            return elementDescriptors;
        }
    }
}
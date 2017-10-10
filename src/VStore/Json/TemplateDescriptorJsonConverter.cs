using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Json
{
    public class TemplateDescriptorJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => typeof(ITemplateDescriptor).IsAssignableFrom(objectType);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var templateDescriptor = (ITemplateDescriptor)value;
            var json = new JObject
                           {
                               [Tokens.PropertiesToken] = templateDescriptor.Properties,
                               [Tokens.ElementsToken] = JArray.FromObject(templateDescriptor.Elements, serializer)
                           };
            json.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var descriptors = obj[Tokens.ElementsToken];
            var elementDescriptors = descriptors.ToObject<IReadOnlyCollection<IElementDescriptor>>(serializer);

            obj.Remove(Tokens.ElementsToken);
            var templateDescriptor = obj.ToObject<TemplateDescriptor>();
            templateDescriptor.Elements = elementDescriptors;

            return templateDescriptor;
        }
    }
}
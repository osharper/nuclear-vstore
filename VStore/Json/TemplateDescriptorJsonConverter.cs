using System;
using System.Collections.Generic;
using System.Reflection;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Json
{
    public class TemplateDescriptorJsonConverter : JsonConverter
    {
        private const string PropertiesToken = "properties";
        private const string ElementDescriptorsToken = "elements";
        private const string ElementDescriptorTypeToken = "type";

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var templateDescriptor = (ITemplateDescriptor)value;

            var json = new JObject
                           {
                               [PropertiesToken] = templateDescriptor.Properties,
                               [ElementDescriptorsToken] = JArray.FromObject(templateDescriptor.Elements, serializer)
                           };
            json.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (typeof(ITemplateDescriptor).IsAssignableFrom(objectType))
            {
                var obj = JObject.Load(reader);
                var descriptors = obj[ElementDescriptorsToken];
                var elementDescriptors = DeserializeElementDescriptors(descriptors);

                obj.Remove(ElementDescriptorsToken);
                var templateDescriptor = obj.ToObject<TemplateDescriptor>();
                templateDescriptor.Elements = elementDescriptors;

                return templateDescriptor;
            }

            if (objectType == typeof(IReadOnlyCollection<IElementDescriptor>))
            {
                var array = JArray.Load(reader);
                return DeserializeElementDescriptors(array);
            }

            throw new ArgumentOutOfRangeException(nameof(objectType));
        }

        public override bool CanConvert(Type objectType) => typeof(ITemplateDescriptor).IsAssignableFrom(objectType) || objectType == typeof(IReadOnlyCollection<IElementDescriptor>);

        private static IReadOnlyCollection<IElementDescriptor> DeserializeElementDescriptors(JToken token)
        {
            var elementDescriptors = new List<IElementDescriptor>();
            foreach (var descriptor in token)
            {
                var type = descriptor[ElementDescriptorTypeToken].ToString();
                var descriptorType = (ElementDescriptorType)Enum.Parse(typeof(ElementDescriptorType), type, true);

                var elementDescriptor = Deserialize(descriptor, descriptorType);
                if (elementDescriptor == null)
                {
                    return Array.Empty<IElementDescriptor>();
                }

                elementDescriptors.Add(elementDescriptor);
            }

            return elementDescriptors;
        }

        private static IElementDescriptor Deserialize(JToken token, ElementDescriptorType descriptorType)
        {
            switch (descriptorType)
            {
                case ElementDescriptorType.Text:
                    return token.ToObject<TextElementDescriptor>();
                case ElementDescriptorType.Image:
                    return token.ToObject<ImageElementDescriptor>();
                case ElementDescriptorType.Article:
                    return token.ToObject<ArticleElementDescriptor>();
                case ElementDescriptorType.FasComment:
                    return token.ToObject<FasCommantElementDescriptor>();
                case ElementDescriptorType.Date:
                    return token.ToObject<DateElementDescriptor>();
                case ElementDescriptorType.Link:
                    return token.ToObject<LinkElementDescriptor>();
                default:
                    return null;
            }
        }
    }
}
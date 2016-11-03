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
        private const string ElementsToken = "elements";
        private const string TypeToken = "type";
        private const string TemplateCodeToken = "templateCode";
        private const string ConstraintsToken = "constraints";

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var templateDescriptor = (ITemplateDescriptor)value;

            var json = new JObject
                           {
                               [PropertiesToken] = templateDescriptor.Properties,
                               [ElementsToken] = JArray.FromObject(templateDescriptor.Elements, serializer)
                           };
            json.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (typeof(ITemplateDescriptor).IsAssignableFrom(objectType))
            {
                var obj = JObject.Load(reader);
                var descriptors = obj[ElementsToken];
                var elementDescriptors = DeserializeElementDescriptors(descriptors);

                obj.Remove(ElementsToken);
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
                var type = descriptor[TypeToken].ToString();
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
            var templateCode = token[TemplateCodeToken].ToObject<int>();
            var properties = (JObject)token[PropertiesToken];
            var constraints = token[ConstraintsToken];
            switch (descriptorType)
            {
                case ElementDescriptorType.Text:
                    return new TextElementDescriptor(templateCode, properties, constraints.ToObject<TextElementConstraints>());
                case ElementDescriptorType.Image:
                    return new ImageElementDescriptor(templateCode, properties, constraints.ToObject<ImageElementConstraints>());
                case ElementDescriptorType.Article:
                    return new ArticleElementDescriptor(templateCode, properties, constraints.ToObject<ArticleElementConstraints>());
                case ElementDescriptorType.FasComment:
                    return new FasCommantElementDescriptor(templateCode, properties, constraints.ToObject<TextElementConstraints>());
                case ElementDescriptorType.Date:
                    return new DateElementDescriptor(templateCode, properties);
                case ElementDescriptorType.Link:
                    return new LinkElementDescriptor(templateCode, properties, constraints.ToObject<TextElementConstraints>());
                default:
                    return null;
            }
        }
    }
}
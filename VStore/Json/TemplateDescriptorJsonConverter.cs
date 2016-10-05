using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;

namespace NuClear.VStore.Json
{
    public class TemplateDescriptorJsonConverter : JsonConverter
    {
        private const string ElementDescriptorsToken = "elementDescriptors";
        private const string ElementDescriptorTypeToken = "type";

        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (objectType == typeof(TemplateDescriptor))
            {
                var jObject = JObject.Load(reader);

                var descriptors = jObject[ElementDescriptorsToken];
                jObject.Remove(ElementDescriptorsToken);

                var templateDescriptor = jObject.ToObject<TemplateDescriptor>();
                DeserializeElementDescriptors(descriptors, templateDescriptor.ElementDescriptors);

                return templateDescriptor;
            }

            if (objectType == typeof(IEnumerable<IElementDescriptor>))
            {
                var jArray = JArray.Load(reader);
                var elementDescriptors = new List<IElementDescriptor>();
                DeserializeElementDescriptors(jArray, elementDescriptors);

                return elementDescriptors;
            }

            throw new ArgumentOutOfRangeException();
        }

        public override bool CanConvert(Type objectType)
            => objectType == typeof(TemplateDescriptor) || objectType == typeof(IEnumerable<IElementDescriptor>);

        private static void DeserializeElementDescriptors(JToken token, IList<IElementDescriptor> elementDescriptors)
        {
            foreach (var descriptor in token)
            {
                var type = descriptor[ElementDescriptorTypeToken].ToString();
                var descriptorType = (ElementDescriptorType)Enum.Parse(typeof(ElementDescriptorType), type);

                var elementDescriptor = Deserialize(descriptor, descriptorType);
                if (elementDescriptor == null)
                {
                    return;
                }

                elementDescriptors.Add(elementDescriptor);
            }
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
                default:
                    return null;
            }
        }
    }
}
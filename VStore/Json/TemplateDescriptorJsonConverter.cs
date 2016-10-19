using System;
using System.Collections.Generic;
using System.Reflection;

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
            if (typeof(ITemplateDescriptor).IsAssignableFrom(objectType))
            {
                var jObject = JObject.Load(reader);

                var descriptors = jObject[ElementDescriptorsToken];
                jObject.Remove(ElementDescriptorsToken);

                var templateDescriptor = jObject.ToObject<TemplateDescriptor>();
                foreach (var elementDescriptor in DeserializeElementDescriptors(descriptors))
                {
                    templateDescriptor.AddElementDescriptor(elementDescriptor);
                }

                return templateDescriptor;
            }

            if (objectType == typeof(IReadOnlyCollection<IElementDescriptor>))
            {
                var jArray = JArray.Load(reader);
                return DeserializeElementDescriptors(jArray);
            }

            throw new ArgumentOutOfRangeException(nameof(objectType));
        }

        public override bool CanConvert(Type objectType)
            => typeof(ITemplateDescriptor).IsAssignableFrom(objectType) || objectType == typeof(IReadOnlyCollection<IElementDescriptor>);

        private static IReadOnlyCollection<IElementDescriptor> DeserializeElementDescriptors(JToken token)
        {
            var elementDescriptors = new List<IElementDescriptor>();
            foreach (var descriptor in token)
            {
                var type = descriptor[ElementDescriptorTypeToken].ToString();
                var descriptorType = (ElementDescriptorType)Enum.Parse(typeof(ElementDescriptorType), type);

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
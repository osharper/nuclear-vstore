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
        private const string ValueToken = "value";

        public override bool CanConvert(Type objectType) => typeof(IObjectElementDescriptor).IsAssignableFrom(objectType);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var objectElementDescriptor = (IObjectElementDescriptor)value;
            var elementDescriptor = ConvertToElementDescriptor(objectElementDescriptor);

            var json = JObject.FromObject(elementDescriptor, serializer);
            json[ValueToken] = JToken.FromObject(objectElementDescriptor.Value, serializer);

            json.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var json = JObject.Load(reader);

            var valueToken = json[ValueToken];
            var elementDescriptor = json.ToObject<IElementDescriptor>(serializer);

            var value = valueToken.AsObjectElementValue(elementDescriptor.Type);

            return new ObjectElementDescriptor(elementDescriptor, value);
        }

        private static IElementDescriptor ConvertToElementDescriptor(IElementDescriptor objectElementDescriptor)
        {
            switch (objectElementDescriptor.Type)
            {
                case ElementDescriptorType.Text:
                    return new TextElementDescriptor(
                               objectElementDescriptor.TemplateCode,
                               objectElementDescriptor.Properties,
                               (TextElementConstraints)objectElementDescriptor.Constraints);
                case ElementDescriptorType.Image:
                    return new ImageElementDescriptor(
                               objectElementDescriptor.TemplateCode,
                               objectElementDescriptor.Properties,
                               (ImageElementConstraints)objectElementDescriptor.Constraints);
                case ElementDescriptorType.Article:
                    return new ArticleElementDescriptor(
                               objectElementDescriptor.TemplateCode,
                               objectElementDescriptor.Properties,
                               (ArticleElementConstraints)objectElementDescriptor.Constraints);
                case ElementDescriptorType.FasComment:
                    return new FasCommantElementDescriptor(
                               objectElementDescriptor.TemplateCode,
                               objectElementDescriptor.Properties,
                               (TextElementConstraints)objectElementDescriptor.Constraints);
                case ElementDescriptorType.Date:
                    return new DateElementDescriptor(objectElementDescriptor.TemplateCode, objectElementDescriptor.Properties);
                case ElementDescriptorType.Link:
                    return new LinkElementDescriptor(
                               objectElementDescriptor.TemplateCode,
                               objectElementDescriptor.Properties,
                               (TextElementConstraints)objectElementDescriptor.Constraints);
                default:
                    return null;
            }
        }
    }
}
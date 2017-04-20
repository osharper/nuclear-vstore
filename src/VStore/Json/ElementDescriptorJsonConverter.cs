using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Json
{
    public sealed class ElementDescriptorJsonConverter : JsonConverter
    {
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(IElementDescriptor);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var type = obj[Tokens.TypeToken].ToString();
            var descriptorType = (ElementDescriptorType)Enum.Parse(typeof(ElementDescriptorType), type, true);
            return DeserializeElementDescriptor(obj, descriptorType);
        }

        private static IElementDescriptor DeserializeElementDescriptor(JToken token, ElementDescriptorType descriptorType)
        {
            var templateCode = token[Tokens.TemplateCodeToken].ToObject<int>();
            var properties = (JObject)token[Tokens.PropertiesToken];
            var constraintSet = token[Tokens.ConstraintsToken];
            return new ElementDescriptor(descriptorType, templateCode, properties, DeserializeConstraintSet(constraintSet, descriptorType));
        }

        private static ConstraintSet DeserializeConstraintSet(JToken token, ElementDescriptorType descriptorType)
        {
            var constraintSetItems = new List<ConstraintSetItem>();
            foreach (var item in token)
            {
                if (item.Type != JTokenType.Property)
                {
                    throw new FormatException($"Template element of type {descriptorType} constraints are malformed.");
                }

                var property = (JProperty)item;
                var language = (Language)Enum.Parse(typeof(Language), property.Name, true);

                IElementConstraints constraints;
                switch (descriptorType)
                {
                    case ElementDescriptorType.Text:
                        if (property.Value.Value<bool?>(Tokens.IsFormattedToken) ?? false)
                        {
                            constraints = property.Value.ToObject<FormattedTextElementConstraints>();
                        }
                        else
                        {
                            constraints = property.Value.ToObject<PlainTextElementConstraints>();
                        }

                        break;
                    case ElementDescriptorType.Image:
                        constraints = property.Value.ToObject<ImageElementConstraints>();
                        break;
                    case ElementDescriptorType.Article:
                        constraints = property.Value.ToObject<ArticleElementConstraints>();
                        break;
                    case ElementDescriptorType.FasComment:
                        constraints = property.Value.ToObject<PlainTextElementConstraints>();
                        break;
                    case ElementDescriptorType.Link:
                        constraints = property.Value.ToObject<LinkElementConstraints>();
                        break;
                    case ElementDescriptorType.Date:
                        constraints = property.Value.ToObject<DateElementConstraints>();
                        break;
                    default:
                        return null;
                }

                constraintSetItems.Add(new ConstraintSetItem(language, constraints));
            }

            return new ConstraintSet(constraintSetItems);
        }
    }
}

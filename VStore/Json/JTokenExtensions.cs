using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Json
{
    public static class JTokenExtensions
    {
        public static IObjectElementValue AsObjectElementValue(this JToken valueToken, ElementDescriptorType elementDescriptorType)
        {
            switch (elementDescriptorType)
            {
                case ElementDescriptorType.Text:
                    return valueToken.ToObject<TextElementValue>();
                case ElementDescriptorType.Image:
                    return valueToken.ToObject<ImageElementValue>();
                case ElementDescriptorType.Article:
                    return valueToken.ToObject<ArticleElementValue>();
                case ElementDescriptorType.FasComment:
                    return valueToken.ToObject<FasElementValue>();
                case ElementDescriptorType.Date:
                    return valueToken.ToObject<TextElementValue>();
                case ElementDescriptorType.Link:
                    return valueToken.ToObject<TextElementValue>();
                default:
                    return null;
            }
        }
    }
}

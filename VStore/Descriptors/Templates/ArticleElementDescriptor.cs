using Newtonsoft.Json.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class ArticleElementDescriptor : IElementDescriptor
    {
        // ReSharper disable once SuggestBaseTypeForParameter
        public ArticleElementDescriptor(int templateCode, JObject properties, ArticleElementConstraints constraints)
        {
            TemplateCode = templateCode;
            Properties = properties;
            Constraints = constraints;
        }

        public ElementDescriptorType Type => ElementDescriptorType.Article;

        public int TemplateCode { get; }

        public JObject Properties { get; }

        public IConstraintSet Constraints { get; }
    }
}
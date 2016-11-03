using Newtonsoft.Json.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class LinkElementDescriptor : IElementDescriptor
    {
        // ReSharper disable once SuggestBaseTypeForParameter
        public LinkElementDescriptor(int templateCode, JObject properties, TextElementConstraints constraints)
        {
            TemplateCode = templateCode;
            Properties = properties;
            Constraints = constraints;
        }

        public ElementDescriptorType Type => ElementDescriptorType.Link;

        public int TemplateCode { get; }

        public JObject Properties { get; }

        public IConstraintSet Constraints { get; }
    }
}
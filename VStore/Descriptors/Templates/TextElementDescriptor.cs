using Newtonsoft.Json.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class TextElementDescriptor : IElementDescriptor
    {
        // ReSharper disable once SuggestBaseTypeForParameter
        public TextElementDescriptor(int templateCode, JObject properties, TextElementConstraints constraints)
        {
            TemplateCode = templateCode;
            Properties = properties;
            Constraints = constraints;
        }

        public ElementDescriptorType Type => ElementDescriptorType.Text;

        public int TemplateCode { get; }

        public JObject Properties { get; }

        public IConstraintSet Constraints { get; }
    }
}
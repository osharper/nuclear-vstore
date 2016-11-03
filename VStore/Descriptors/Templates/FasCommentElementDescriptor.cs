using Newtonsoft.Json.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class FasCommantElementDescriptor : IElementDescriptor
    {
        // ReSharper disable once SuggestBaseTypeForParameter
        public FasCommantElementDescriptor(int templateCode, JObject properties, TextElementConstraints constraints)
        {
            TemplateCode = templateCode;
            Properties = properties;
            Constraints = constraints;
        }

        public ElementDescriptorType Type => ElementDescriptorType.FasComment;

        public int TemplateCode { get; }

        public JObject Properties { get; set; }

        public IConstraintSet Constraints { get; }
    }
}
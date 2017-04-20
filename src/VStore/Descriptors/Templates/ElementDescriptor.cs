using Newtonsoft.Json.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class ElementDescriptor : IElementDescriptor
    {
        public ElementDescriptor(ElementDescriptorType type, int templateCode, JObject properties, ConstraintSet constraints)
        {
            Type = type;
            TemplateCode = templateCode;
            Properties = properties;
            Constraints = constraints;
        }

        public ElementDescriptorType Type { get; }

        public int TemplateCode { get; }

        public JObject Properties { get; }

        public ConstraintSet Constraints { get; }
    }
}
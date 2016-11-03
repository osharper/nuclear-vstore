using Newtonsoft.Json.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class DateElementDescriptor : IElementDescriptor
    {
        public DateElementDescriptor(int templateCode, JObject properties)
        {
            TemplateCode = templateCode;
            Properties = properties;
        }

        public ElementDescriptorType Type => ElementDescriptorType.Date;

        public int TemplateCode { get; }

        public JObject Properties { get; }

        public IConstraintSet Constraints => null;
    }
}
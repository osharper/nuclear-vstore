using Newtonsoft.Json.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class ImageElementDescriptor : IElementDescriptor
    {
        // ReSharper disable once SuggestBaseTypeForParameter
        public ImageElementDescriptor(int templateCode, JObject properties, ImageElementConstraints constraints)
        {
            TemplateCode = templateCode;
            Properties = properties;
            Constraints = constraints;
        }

        public ElementDescriptorType Type => ElementDescriptorType.Image;

        public int TemplateCode { get; }

        public JObject Properties { get; }

        public IConstraintSet Constraints { get; }
    }
}
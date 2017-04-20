using Newtonsoft.Json.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public interface IElementDescriptor : IDescriptor
    {
        ElementDescriptorType Type { get; }
        int TemplateCode { get; }
        JObject Properties { get; }
        ConstraintSet Constraints { get; }
    }
}
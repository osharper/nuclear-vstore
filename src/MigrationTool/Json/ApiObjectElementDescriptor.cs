using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;

namespace MigrationTool.Json
{
    public class ApiObjectElementDescriptor : IObjectElementDescriptor
    {
        public ElementDescriptorType Type { get; set; }
        public int TemplateCode { get; set; }
        public JObject Properties { get; set; }
        public ConstraintSet Constraints { get; set; }
        public long Id { get; set; }
        public IObjectElementValue Value { get; set; }
        public string UploadUrl { get; set; }
    }
}

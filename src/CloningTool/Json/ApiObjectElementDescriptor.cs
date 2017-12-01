using System;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;

namespace CloningTool.Json
{
    public class ApiObjectElementDescriptor : IVersionedObjectElementDescriptor
    {
        public ElementDescriptorType Type { get; set; }
        public int TemplateCode { get; set; }
        public JObject Properties { get; set; }
        public ConstraintSet Constraints { get; set; }
        public long Id { get; set; }
        public IObjectElementValue Value { get; set; }
        public string UploadUrl { get; set; }
        public string VersionId { get; set; }
        public DateTime LastModified { get; set; }
    }
}

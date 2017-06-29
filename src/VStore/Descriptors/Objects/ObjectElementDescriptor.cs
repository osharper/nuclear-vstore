using System;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors.Objects
{
    public sealed class ObjectElementDescriptor : IVersionedObjectElementDescriptor
    {
        public long Id { get; set; }
        public string VersionId { get; set; }
        public DateTime LastModified { get; set; }
        public ElementDescriptorType Type { get; set; }
        public int TemplateCode { get; set; }
        public JObject Properties { get; set; }
        public ConstraintSet Constraints { get; set; }
        public IObjectElementValue Value { get; set; }
    }
}
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace NuClear.VStore.Descriptors.Objects
{
    public sealed class ObjectPersistenceDescriptor : IObjectPersistenceDescriptor
    {
        public long TemplateId { get; set; }
        public string TemplateVersionId { get; set; }
        public Language Language { get; set; }
        public JObject Properties { get; set; }
        public IReadOnlyCollection<VersionedObjectDescriptor<string>> Elements { get; set; }
    }
}
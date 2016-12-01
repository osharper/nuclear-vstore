using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using NuClear.VStore.S3;

namespace NuClear.VStore.Descriptors.Objects
{
    public sealed class ObjectPersistenceDescriptor : IObjectPersistenceDescriptor
    {
        public long TemplateId { get; set; }

        public string TemplateVersionId { get; set; }

        public JObject Properties { get; set; }

        public IReadOnlyCollection<S3ObjectVersion> Elements { get; set; }
    }
}
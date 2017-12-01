using System;

using NuClear.VStore.Descriptors;

namespace CloningTool.Json
{
    public class ApiVersionedDescriptor : IVersioned
    {
        public string VersionId { get; set; }
        public DateTime LastModified { get; set; }
    }
}

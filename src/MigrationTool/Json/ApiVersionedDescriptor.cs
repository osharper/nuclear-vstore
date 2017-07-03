using System;

using NuClear.VStore.Descriptors;

namespace MigrationTool.Json
{
    public class ApiVersionedDescriptor : IVersioned
    {
        public string VersionId { get; set; }
        public DateTime LastModified { get; set; }
    }
}

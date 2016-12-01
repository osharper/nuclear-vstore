using System;

namespace NuClear.VStore.S3
{
    public sealed class S3ObjectVersion
    {
        public string Key { get; set; }
        public string VersionId { get; set; }
        public DateTime LastModified { get; set; }
    }
}
using System;

namespace NuClear.VStore.Host.Options
{
    public sealed class LockOptions
    {
        public string BucketName { get; set; }
        public TimeSpan Expiration { get; set; }
    }
}
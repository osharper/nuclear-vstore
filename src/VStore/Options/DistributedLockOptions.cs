using System;

namespace NuClear.VStore.Options
{
    public sealed class DistributedLockOptions
    {
        public bool DeveloperMode { get; set; }
        public string EndPoints { get; set; }
        public string Password { get; set; }
        public TimeSpan Expiration { get; set; }
    }
}
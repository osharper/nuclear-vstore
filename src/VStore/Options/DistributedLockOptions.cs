using System;

namespace NuClear.VStore.Options
{
    public sealed class DistributedLockOptions
    {
        public bool DeveloperMode { get; set; }
        public string EndPoints { get; set; }
        public int? ConnectionTimeout { get; set; }
        public int? SyncTimeout { get; set; }
        public int? KeepAlive { get; set; }
        public string Password { get; set; }
        public TimeSpan Expiration { get; set; }
    }
}
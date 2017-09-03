using System;
using System.Collections.Generic;

namespace NuClear.VStore.Options
{
    public sealed class DistributedLockOptions
    {
        public bool DeveloperMode { get; set; }
        public IEnumerable<DnsEndPoint> EndPoints { get; set; }
        public TimeSpan Expiration { get; set; }

        public class DnsEndPoint
        {
            public string Host { get; set; }
            public int Port { get; set; }
        }
    }
}
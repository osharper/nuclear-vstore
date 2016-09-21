using System;

namespace NuClear.VStore.Host.Options
{
    public sealed class CephOptions
    {
        public Uri EndpointUrl { get; set; }
        public string BucketName { get; set; }
        public string AccessKey { get; set; }
        public string SecretKey { get; set; }
    }
}
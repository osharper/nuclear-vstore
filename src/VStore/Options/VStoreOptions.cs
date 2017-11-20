using System;

namespace NuClear.VStore.Options
{
    public sealed class VStoreOptions
    {
        public TimeSpan SessionExpiration { get; set; }
        public Uri FileStorageEndpoint { get; set; }
        public Uri PreviewEndpoint { get; set; }

        public long MaxBinarySize { get; set; }
    }
}

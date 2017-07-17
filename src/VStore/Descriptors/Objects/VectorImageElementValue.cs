using System;

namespace NuClear.VStore.Descriptors.Objects
{
    public class VectorImageElementValue : IBinaryElementValue
    {
        public string Raw { get; set; }
        public string Filename { get; set; }
        public long? Filesize { get; set; }
        public Uri DownloadUri { get; set; }
    }
}

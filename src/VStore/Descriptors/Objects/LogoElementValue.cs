using System;
using System.Collections.Generic;

using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors.Objects
{
    public sealed class LogoElementValue : ILogoElementValue
    {
        public string Raw { get; }
        public string Filename { get; }
        public long? Filesize { get; }

        public Uri DownloadUri { get; set; }
        public Uri PreviewUri { get; set; }

        public CropShape CropShape { get; set; }
        public CropArea CropArea { get; set; }
        public IEnumerable<CustomImage> CustomImages { get; set; }
    }
}
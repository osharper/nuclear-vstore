using System.Collections.Generic;

namespace NuClear.VStore.Descriptors.Objects.Persistence
{
    public class LogoElementPersistenceValue : IBinaryElementPersistenceValue
    {
        public LogoElementPersistenceValue(string raw, string filename, long? filesize, CropArea cropArea, IEnumerable<CustomImage> customImages)
        {
            Raw = raw;
            Filename = filename;
            Filesize = filesize;
            CropArea = cropArea;
            CustomImages = customImages;
        }

        public string Raw { get; }
        public string Filename { get; }
        public long? Filesize { get; }

        public CropArea CropArea { get; set; }
        public IEnumerable<CustomImage> CustomImages { get; set; }

        public class CustomImage
        {
            public ImageSize Size { get; set; }
            public string Raw { get; set; }

            public string Filename { get; set; }
            public long? Filesize { get; set; }
        }
    }
}
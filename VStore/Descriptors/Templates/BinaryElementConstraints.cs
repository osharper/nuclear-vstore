using System.Collections.Generic;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class ImageElementConstraints : IBinaryConstraintSet
    {
        public int? MaxSize { get; set; }
        public int? MaxFilenameLength { get; set; }
        public IEnumerable<FileFormat> SupportedFileFormats { get; set; }
        public ImageSize ImageSize { get; set; }
        public bool IsAlphaChannelRequired { get; set; }
    }
}
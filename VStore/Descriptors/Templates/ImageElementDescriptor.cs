using System.Collections.Generic;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class ImageElementDescriptor : IBinaryElementDescriptor
    {
        public ElementDescriptorType Type => ElementDescriptorType.Image;
        public int? MaxSize { get; set; }
        public int? MaxFilenameLenght { get; set; }
        public IEnumerable<FileFormat> SupportedFileFormats { get; set; }
        public ImageSize ImageSize { get; set; }
        public bool IsAlphaChannelRequired { get; set; }
    }
}
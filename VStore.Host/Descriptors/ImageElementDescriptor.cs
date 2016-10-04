using System.Collections.Generic;

namespace NuClear.VStore.Host.Descriptors
{
    public sealed class ImageElementDescriptor : IBinaryElementDescriptor
    {
        public ElementDescriptorType Type => ElementDescriptorType.Image;
        public bool IsMandatory { get; set; }
        public int? MaxSize { get; set; }
        public int? MaxFilenameLenght { get; set; }
        public IEnumerable<SupportedFileFormat> SupportedFileFormats { get; set; }
        public ImageSize? ImageSize { get; set; }
        public bool IsAlphaChannelRequired { get; set; }
    }
}
using System.Collections.Generic;

namespace NuClear.VStore.Descriptors.Templates
{
    public interface IBinaryElementDescriptor : IElementDescriptor
    {
        int? MaxSize { get; set; }
        int? MaxFilenameLenght { get; set; }
        IEnumerable<FileFormat> SupportedFileFormats { get; set; }
    }
}
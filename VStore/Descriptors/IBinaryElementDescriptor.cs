using System.Collections.Generic;

namespace NuClear.VStore.Descriptors
{
    public interface IBinaryElementDescriptor : IElementDescriptor
    {
        int? MaxSize { get; set; }
        int? MaxFilenameLenght { get; set; }
        IEnumerable<SupportedFileFormat> SupportedFileFormats { get; set; }
    }
}
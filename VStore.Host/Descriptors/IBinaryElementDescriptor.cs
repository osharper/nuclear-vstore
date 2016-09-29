using System.Collections.Generic;

namespace NuClear.VStore.Host.Descriptors
{
    public interface IBinaryElementDescriptor : IElementDescriptor
    {
        int? MaxSize { get; set; }
        int? MaxFilenameLenght { get; set; }
        IEnumerable<SupportedFileFormat> SupportedFileFormats { get; set; }
    }
}
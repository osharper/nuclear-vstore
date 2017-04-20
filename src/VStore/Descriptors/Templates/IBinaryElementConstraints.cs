using System.Collections.Generic;

namespace NuClear.VStore.Descriptors.Templates
{
    public interface IBinaryElementConstraints : IElementConstraints
    {
        int? MaxSize { get; set; }
        int? MaxFilenameLength { get; set; }
        IEnumerable<FileFormat> SupportedFileFormats { get; set; }
        bool BinaryExists { get; }
    }
}

using System.Collections.Generic;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class ArticleElementConstraints : IBinaryElementConstraints
    {
        public int? MaxSize { get; set; }
        public int? MaxFilenameLenght { get; set; }
        public IEnumerable<FileFormat> SupportedFileFormats { get; set; }
    }
}
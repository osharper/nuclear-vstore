using System.Collections.Generic;

namespace NuClear.VStore.Descriptors
{
    public sealed class ArticleElementDescriptor : IBinaryElementDescriptor
    {
        public ElementDescriptorType Type => ElementDescriptorType.Article;
        public bool IsMandatory { get; set; }
        public int? MaxSize { get; set; }
        public int? MaxFilenameLenght { get; set; }
        public IEnumerable<SupportedFileFormat> SupportedFileFormats { get; set; }
    }
}
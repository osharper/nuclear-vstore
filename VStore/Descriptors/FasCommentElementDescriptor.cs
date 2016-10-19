using System.Collections.Generic;

namespace NuClear.VStore.Descriptors
{
    public sealed class FasCommantElementDescriptor : ITextElementDescriptor
    {
        public ElementDescriptorType Type => ElementDescriptorType.FasComment;
        public bool IsMandatory { get; set; }
        public int? MaxSymbols { get; set; }
    }
}
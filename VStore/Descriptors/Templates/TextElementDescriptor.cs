namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class TextElementDescriptor : ITextElementDescriptor
    {
        public ElementDescriptorType Type => ElementDescriptorType.Text;
        public int? MaxSymbols { get; set; }
        public int? MaxSymbolsPerWord { get; set; }
        public int? MaxLines { get; set; }
        public bool IsFormatted { get; set; }
    }
}
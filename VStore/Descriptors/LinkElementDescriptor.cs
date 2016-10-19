namespace NuClear.VStore.Descriptors
{
    public sealed class LinkElementDescriptor : ITextElementDescriptor
    {
        public ElementDescriptorType Type => ElementDescriptorType.Link;
        public bool IsMandatory { get; set; }
        public int? MaxSymbols { get; set; }
    }
}
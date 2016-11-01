namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class LinkElementDescriptor : ITextElementDescriptor
    {
        public ElementDescriptorType Type => ElementDescriptorType.Link;

        public int? MaxSymbols { get; set; }
    }
}
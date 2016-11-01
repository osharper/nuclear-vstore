namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class FasCommantElementDescriptor : ITextElementDescriptor
    {
        public ElementDescriptorType Type => ElementDescriptorType.FasComment;

        public int? MaxSymbols { get; set; }
    }
}
namespace NuClear.VStore.Descriptors
{
    public sealed class DateElementDescriptor : IElementDescriptor
    {
        public ElementDescriptorType Type => ElementDescriptorType.Date;
        public bool IsMandatory { get; set; }
    }
}
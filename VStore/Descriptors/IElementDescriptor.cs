namespace NuClear.VStore.Descriptors
{
    public interface IElementDescriptor : IDescriptor
    {
        ElementDescriptorType Type { get; }
        bool IsMandatory { get; set; }
    }
}
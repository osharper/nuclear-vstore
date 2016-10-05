namespace NuClear.VStore.Descriptors
{
    public interface IElementDescriptor
    {
        ElementDescriptorType Type { get; }
        bool IsMandatory { get; set; }
    }
}
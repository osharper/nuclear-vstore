namespace NuClear.VStore.Host.Descriptors
{
    public interface IElementDescriptor
    {
        ElementDescriptorType Type { get; }
        bool IsMandatory { get; set; }
    }
}
namespace NuClear.VStore.Descriptors.Templates
{
    public interface IElementDescriptor : IDescriptor
    {
        ElementDescriptorType Type { get; }
    }
}
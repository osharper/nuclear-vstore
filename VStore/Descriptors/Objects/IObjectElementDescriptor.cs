using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors.Objects
{
    public interface IObjectElementDescriptor : IElementDescriptor
    {
        long Id { get; }
        IObjectElementValue Value { get; }
    }
}
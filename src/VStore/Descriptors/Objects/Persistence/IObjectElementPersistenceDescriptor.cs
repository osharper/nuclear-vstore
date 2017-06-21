using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors.Objects.Persistence
{
    public interface IObjectElementPersistenceDescriptor : IElementDescriptor
    {
        IObjectElementValue Value { get; }
    }
}
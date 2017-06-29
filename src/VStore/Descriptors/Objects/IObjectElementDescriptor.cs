using NuClear.VStore.Descriptors.Objects.Persistence;

namespace NuClear.VStore.Descriptors.Objects
{
    public interface IObjectElementDescriptor : IObjectElementPersistenceDescriptor, IIdentifyable<long>
    {
    }
}
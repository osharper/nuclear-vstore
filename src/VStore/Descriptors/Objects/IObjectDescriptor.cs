using System.Collections.Generic;

using NuClear.VStore.Descriptors.Objects.Persistence;

namespace NuClear.VStore.Descriptors.Objects
{
    public interface IObjectDescriptor : IObjectPersistenceDescriptor
    {
        IReadOnlyCollection<IObjectElementDescriptor> Elements { get; set; }
    }
}

using System.Collections.Generic;

namespace NuClear.VStore.Descriptors.Objects
{
    public interface IObjectDescriptor : IObjectPersistenceDescriptor
    {
        IReadOnlyCollection<IObjectElementDescriptor> Elements { get; set; }
    }
}

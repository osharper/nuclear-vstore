using System.Collections.Generic;

namespace NuClear.VStore.Descriptors.Objects
{
    public interface IObjectDescriptor : IObjectPersistenceDescriptor
    {
        string Author { get; set; }
        string AuthorLogin { get; set; }
        string AuthorName { get; set; }
        IReadOnlyCollection<IObjectElementDescriptor> Elements { get; set; }
    }
}

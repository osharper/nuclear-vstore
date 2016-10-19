using System;
using System.Collections.Generic;

namespace NuClear.VStore.Descriptors
{
    public interface ITemplateDescriptor : IDescriptor
    {
        Guid Id { get; set; }
        string Name { get; set; }
        bool IsMandatory { get; set; }
        IReadOnlyCollection<IElementDescriptor> ElementDescriptors { get; }
    }
}
using System;
using System.Collections.Generic;

namespace NuClear.VStore.Descriptors
{
    public interface ITemplateDescriptor
    {
        Guid Id { get; set; }
        string Name { get; set; }
        bool IsMandatory { get; set; }
        IList<IElementDescriptor> ElementDescriptors { get; }
    }
}
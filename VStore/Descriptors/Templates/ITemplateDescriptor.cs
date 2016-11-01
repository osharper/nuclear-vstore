using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public interface ITemplateDescriptor : IDescriptor
    {
        JObject Properties { get; set; }
        IReadOnlyCollection<IElementDescriptor> Elements { get; }
    }
}
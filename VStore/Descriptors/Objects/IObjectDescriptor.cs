using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace NuClear.VStore.Descriptors.Objects
{
    public interface IObjectDescriptor : IDescriptor
    {
        long TemplateId { get; }
        string TemplateVersionId { get; }
        JObject Properties { get; set; }
        IReadOnlyCollection<IObjectElementDescriptor> Elements { get; set; }
    }
}

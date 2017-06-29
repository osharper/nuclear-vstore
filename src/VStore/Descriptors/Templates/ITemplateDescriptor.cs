using System.Collections.Generic;

using Newtonsoft.Json.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public interface ITemplateDescriptor : IDescriptor
    {
        string Author { get; set; }
        string AuthorLogin { get; set; }
        string AuthorName { get; set; }
        JObject Properties { get; set; }
        IReadOnlyCollection<IElementDescriptor> Elements { get; }
    }
}
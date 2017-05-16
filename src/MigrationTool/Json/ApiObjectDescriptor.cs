using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;

namespace MigrationTool.Json
{
    public class ApiObjectDescriptor
    {
        public long Id { get; set; }
        public IReadOnlyCollection<ApiObjectElementDescriptor> Elements { get; set; }
        public long TemplateId { get; set; }
        public string TemplateVersionId { get; set; }
        public Language Language { get; set; }
        public JObject Properties { get; set; }
    }
}

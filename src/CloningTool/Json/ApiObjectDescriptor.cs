using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;

namespace CloningTool.Json
{
    public class ApiObjectDescriptor : IVersioned
    {
        public long Id { get; set; }
        public IReadOnlyCollection<ApiObjectElementDescriptor> Elements { get; set; }
        public long TemplateId { get; set; }
        public string TemplateVersionId { get; set; }
        public Language Language { get; set; }
        public JObject Properties { get; set; }
        public string VersionId { get; set; }
        public DateTime LastModified { get; set; }
        public FirmDescriptor Firm { get; set; }
    }
}

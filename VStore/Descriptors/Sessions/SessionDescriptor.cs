using System;
using System.Collections.Generic;

namespace NuClear.VStore.Descriptors.Sessions
{
    public sealed class SessionDescriptor : IDescriptor
    {
        public long TemplateId { get; set; }
        public string TemplateVersionId { get; set; }
        public IEnumerable<int> BinaryElementTemplateCodes { get; set; }
    }
}
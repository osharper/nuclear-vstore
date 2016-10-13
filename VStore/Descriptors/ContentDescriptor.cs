using System;
using System.Collections.Generic;

namespace NuClear.VStore.Descriptors
{
    public sealed class ContentDescriptor
    {
        public long Id { get; set; }
        public string VersionId { get; set; }
        public DateTime LastModified { get; set; }
        public IReadOnlyCollection<IContentElementDescriptor> ContentElementDescriptors { get; set; }
        public IVersionedTemplateDescriptor TemplateDescriptor { get; set; }
    }
}
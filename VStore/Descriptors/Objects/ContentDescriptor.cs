using System;
using System.Collections.Generic;

using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors.Objects
{
    public sealed class ContentDescriptor : IDescriptor, IIdentityable, IVersioned
    {
        public long Id { get; set; }
        public string VersionId { get; set; }
        public DateTime LastModified { get; set; }
        public string Name { get; set; }
        public IReadOnlyCollection<IContentElementDescriptor> ContentElementDescriptors { get; set; }
        public IVersionedTemplateDescriptor TemplateDescriptor { get; set; }
    }
}
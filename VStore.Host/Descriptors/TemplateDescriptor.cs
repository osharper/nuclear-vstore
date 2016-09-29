using System;
using System.Collections.Generic;

namespace NuClear.VStore.Host.Descriptors
{
    public struct TemplateDescriptor : IEquatable<TemplateDescriptor>
    {
        public TemplateDescriptor(Guid id, string versionId, string name, IEnumerable<IElementDescriptor> elementDescriptors)
        {
            Id = id;
            VersionId = versionId;
            Name = name;
            ElementDescriptors = elementDescriptors;
        }

        public Guid Id { get; }

        public string VersionId { get; }

        public string Name { get; }

        public IEnumerable<IElementDescriptor> ElementDescriptors { get; }

        public static bool operator ==(TemplateDescriptor descriptor1, TemplateDescriptor descriptor2)
            => descriptor1.Id == descriptor2.Id &&
               string.Equals(descriptor1.VersionId, descriptor2.VersionId, StringComparison.OrdinalIgnoreCase);

        public static bool operator !=(TemplateDescriptor descriptor1, TemplateDescriptor descriptor2)
            => !(descriptor1 == descriptor2);

        public override bool Equals(object obj) => obj is TemplateDescriptor && Equals((TemplateDescriptor)obj);

        public bool Equals(TemplateDescriptor other) => this == other;

        public override int GetHashCode()
        {
            unchecked
            {
                return (Id.GetHashCode() * 397) ^ (VersionId?.GetHashCode() ?? 0);
            }
        }
    }
}
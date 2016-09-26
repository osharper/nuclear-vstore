using System;

namespace NuClear.VStore.Host.Model
{
    public struct TemplateDescriptor : IEquatable<TemplateDescriptor>
    {
        public TemplateDescriptor(string key, string versionId, string name)
        {
            Key = key;
            VersionId = versionId;
            Name = name;
        }

        public string Key { get; }

        public string VersionId { get; }

        public string Name { get; }

        public static bool operator ==(TemplateDescriptor descriptor1, TemplateDescriptor descriptor2)
            => string.Equals(descriptor1.Key, descriptor2.Key, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(descriptor1.VersionId, descriptor2.VersionId, StringComparison.OrdinalIgnoreCase);

        public static bool operator !=(TemplateDescriptor descriptor1, TemplateDescriptor descriptor2)
            => !(descriptor1 == descriptor2);

        public override bool Equals(object obj) => obj is TemplateDescriptor && Equals((TemplateDescriptor)obj);

        public bool Equals(TemplateDescriptor other) => this == other;

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Key?.GetHashCode() ?? 0) * 397) ^ (VersionId?.GetHashCode() ?? 0);
            }
        }
    }
}
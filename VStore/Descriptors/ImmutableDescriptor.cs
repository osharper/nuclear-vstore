using System;

namespace NuClear.VStore.Descriptors
{
    public sealed class ImmutableDescriptor : IIdentityable, IVersioned, IEquatable<ImmutableDescriptor>
    {
        public ImmutableDescriptor(long id, string versionId, DateTime lastModified)
        {
            Id = id;
            VersionId = versionId;
            LastModified = lastModified;
        }

        public ImmutableDescriptor(string id, string versionId, DateTime lastModified)
            : this(long.Parse(id), versionId, lastModified)
        {
        }

        public long Id { get; }

        public string VersionId { get; }

        public DateTime LastModified { get; }

        public override bool Equals(object obj)
        {
            var other = obj as ImmutableDescriptor;
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Id == other.Id && (VersionId?.Equals(other.VersionId, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        public bool Equals(ImmutableDescriptor other) => other.Equals(this);

        public override int GetHashCode()
        {
            unchecked
            {
                return (Id.GetHashCode() * 397) ^ (VersionId?.GetHashCode() ?? 0);
            }
        }
    }
}
using System;

namespace NuClear.VStore.Descriptors
{
    public sealed class IdentifyableObjectDescriptor<TId> : IIdentifyable<TId>, IEquatable<IdentifyableObjectDescriptor<TId>>
        where TId : IEquatable<TId>
    {
        public IdentifyableObjectDescriptor(TId id, DateTime lastModified)
        {
            Id = id;
            LastModified = lastModified;
        }

        public TId Id { get; }

        public DateTime LastModified { get; }

        public override bool Equals(object obj)
        {
            var other = obj as IdentifyableObjectDescriptor<TId>;
            if (other == null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(other);
        }

        public bool Equals(IdentifyableObjectDescriptor<TId> other) => Id.Equals(other.Id);

        public override int GetHashCode() => Id.GetHashCode();
    }
}
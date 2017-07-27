using System;

using NuClear.VStore.Descriptors;

namespace NuClear.VStore.DataContract
{
    public sealed class IdentifyableObjectRecord<TId> : IIdentifyable<TId>, IEquatable<IdentifyableObjectRecord<TId>>
        where TId : IEquatable<TId>
    {
        public IdentifyableObjectRecord(TId id, DateTime lastModified)
        {
            Id = id;
            LastModified = lastModified;
        }

        public TId Id { get; }

        public DateTime LastModified { get; }

        public override bool Equals(object obj)
        {
            var other = obj as IdentifyableObjectRecord<TId>;
            if (other == null)
            {
                return false;
            }

            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            return Equals(other);
        }

        public bool Equals(IdentifyableObjectRecord<TId> other) => Id.Equals(other.Id);

        public override int GetHashCode() => Id.GetHashCode();
    }
}
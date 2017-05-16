using System;

namespace NuClear.VStore.Descriptors
{
    public sealed class LockSessionDescriptor : IDescriptor
    {
        public Guid UniqueKey { get; set; }
        public DateTime ExpirationDate { get; set; }
    }
}
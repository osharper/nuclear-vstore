using System;
using System.Threading.Tasks;

using NuClear.VStore.Descriptors;

namespace NuClear.VStore.Locks
{
    public sealed class LockSession
    {
        private readonly long _rootObjectId;
        private readonly LockSessionManager _lockSessionManager;

        public LockSession(long rootObjectId, LockSessionManager lockSessionManager)
        {
            _rootObjectId = rootObjectId;
            _lockSessionManager = lockSessionManager;
        }

        public LockSessionDescriptor CreateDescriptor(TimeSpan expiration)
            => new LockSessionDescriptor { ExpirationDate = DateTime.UtcNow.Add(expiration), UniqueKey = Guid.NewGuid() };

        public async Task ReleaseAsync() => await _lockSessionManager.DeleteLockSessionAsync(_rootObjectId);
    }
}

using System;
using System.Threading.Tasks;

using NuClear.VStore.Options;

using RedLockNet;

namespace NuClear.VStore.Locks
{
    public sealed class LockSessionManager
    {
        private static readonly TimeSpan CheckLockExpiration = TimeSpan.FromSeconds(30);

        private readonly IDistributedLockFactory _lockFactory;
        private readonly TimeSpan _expiration;

        public LockSessionManager(IDistributedLockFactory lockFactory, DistributedLockOptions lockOptions)
        {

            _lockFactory = lockFactory;
            _expiration = lockOptions.Expiration;
        }

        public async Task EnsureLockSessionNotExists(long rootObjectKey)
        {
            using (var redLock = await _lockFactory.CreateLockAsync(rootObjectKey.ToString(), CheckLockExpiration))
            {
                if (!redLock.IsAcquired)
                {
                    throw new LockAlreadyExistsException(rootObjectKey);
                }
            }
        }

        public async Task<IRedLock> CreateLockSessionAsync(long rootObjectKey)
        {
            var redLock = await _lockFactory.CreateLockAsync(rootObjectKey.ToString(), _expiration);
            if (!redLock.IsAcquired)
            {
                throw new LockAlreadyExistsException(rootObjectKey);
            }

            return redLock;
        }
    }
}

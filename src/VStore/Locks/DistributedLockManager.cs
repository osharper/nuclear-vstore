using System;
using System.Threading.Tasks;

using NuClear.VStore.Options;

using RedLockNet;

namespace NuClear.VStore.Locks
{
    public sealed class DistributedLockManager
    {
        private static readonly TimeSpan CheckLockExpiration = TimeSpan.FromSeconds(30);

        private readonly IDistributedLockFactory _lockFactory;
        private readonly TimeSpan _expiration;

        public DistributedLockManager(IDistributedLockFactory lockFactory, DistributedLockOptions lockOptions)
        {

            _lockFactory = lockFactory;
            _expiration = lockOptions.Expiration;
        }

        public async Task EnsureLockNotExists(long rootObjectKey)
        {
            using (var redLock = await _lockFactory.CreateLockAsync(rootObjectKey.ToString(), CheckLockExpiration))
            {
                if (!redLock.IsAcquired)
                {
                    throw new LockAlreadyExistsException(rootObjectKey);
                }
            }
        }

        public async Task<IRedLock> CreateLockAsync(long rootObjectKey)
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

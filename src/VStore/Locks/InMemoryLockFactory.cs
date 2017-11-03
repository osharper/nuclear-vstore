using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Caching.Memory;

using RedLockNet;

namespace NuClear.VStore.Locks
{
    /// <summary>
    /// Should be used in development environment only!
    /// </summary>
    public sealed class InMemoryLockFactory : IDistributedLockFactory
    {
        private readonly IMemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());

        public IRedLock CreateLock(string resource, TimeSpan expiryTime)
        {
            return new InMemoryLock(_memoryCache, resource, expiryTime);
        }

        public Task<IRedLock> CreateLockAsync(string resource, TimeSpan expiryTime)
        {
            var redLock = CreateLock(resource, expiryTime);
            return Task.FromResult(redLock);
        }

        public IRedLock CreateLock(string resource, TimeSpan expiryTime, TimeSpan waitTime, TimeSpan retryTime, CancellationToken? cancellationToken = null)
        {
            throw new NotSupportedException();
        }

        public Task<IRedLock> CreateLockAsync(string resource, TimeSpan expiryTime, TimeSpan waitTime, TimeSpan retryTime, CancellationToken? cancellationToken = null)
        {
            throw new NotSupportedException();
        }
    }
}
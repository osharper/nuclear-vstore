using System;

using Microsoft.Extensions.Caching.Memory;

using RedLockNet;

namespace NuClear.VStore.Locks
{
    /// <summary>
    /// Should be used in development environment only!
    /// </summary>
    public sealed class InMemoryLock : IRedLock
    {
        private readonly IMemoryCache _memoryCache;
        private readonly string _resource;

        public string LockId { get; } = Guid.NewGuid().ToString();
        public bool IsAcquired { get; private set; }
        public int ExtendCount { get; } = 0;

        public InMemoryLock(IMemoryCache memoryCache, string resource, TimeSpan expiryTime)
        {
            _memoryCache = memoryCache;
            _resource = resource;

            if (_memoryCache.Get(resource) != null)
            {
                IsAcquired = false;
            }
            else
            {
                _memoryCache.Set(resource, new object(), expiryTime);
                IsAcquired = true;
            }
        }

        public void Dispose()
        {
            _memoryCache.Remove(_resource);
            IsAcquired = false;
        }
    }
}
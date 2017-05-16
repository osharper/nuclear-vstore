using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Locks;

namespace NuClear.VStore.GC.Jobs
{
    public sealed class LockCleanupJob : AsyncJob
    {
        private readonly ILogger<LockCleanupJob> _logger;
        private readonly LockSessionManager _lockSessionManager;

        public LockCleanupJob(ILogger<LockCleanupJob> logger, LockSessionManager lockSessionManager)
        {
            _logger = logger;
            _lockSessionManager = lockSessionManager;
        }

        protected override async Task ExecuteInternalAsync()
        {
            var objectIds = await _lockSessionManager.GetAllCurrentLockSessionsAsync();
            foreach (var objId in objectIds)
            {
                if (await _lockSessionManager.IsLockSessionExpired(objId))
                {
                    await _lockSessionManager.DeleteLockSessionAsync(objId);
                    _logger.LogInformation("Frozen and already expired lock for the object with id = '{id}' has been deleted.", objId);
                }
            }
        }
    }
}

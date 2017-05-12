using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Locks;

namespace NuClear.VStore.GC.Jobs
{
    public sealed class LockCleanupJob : AsyncJob
    {
        private readonly LockSessionManager _lockSessionManager;

        public LockCleanupJob(ILogger<LockCleanupJob> logger, LockSessionManager lockSessionManager)
        {
            _lockSessionManager = lockSessionManager;
        }

        protected override async Task ExecuteInternalAsync()
        {
            var lockIds = await _lockSessionManager.GetAllCurrentLockSessions();
            foreach (var lockId in lockIds)
            {
            }
        }
    }
}

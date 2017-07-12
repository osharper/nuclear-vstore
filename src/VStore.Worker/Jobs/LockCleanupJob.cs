using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using NuClear.VStore.Locks;

namespace NuClear.VStore.Worker.Jobs
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

        protected override async Task ExecuteInternalAsync(IReadOnlyDictionary<string, string[]> args, CancellationToken cancellationToken)
        {
            var objectIds = await _lockSessionManager.GetAllCurrentLockSessionsAsync();
            var tasks = objectIds.Select(
                async x =>
                    {
                        if (await _lockSessionManager.IsLockSessionExpired(x))
                        {
                            await _lockSessionManager.DeleteLockSessionAsync(x);
                            _logger.LogInformation("Expired lock for the object with id = '{id}' has been deleted.", x);
                        }
                    });
            await Task.WhenAll(tasks);
        }
    }
}

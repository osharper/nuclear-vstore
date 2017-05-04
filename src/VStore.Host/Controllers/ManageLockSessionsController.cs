using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Locks;

namespace NuClear.VStore.Host.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/{api-version:apiVersion}/mgmt/lock-sessions")]
    public sealed class ManageLockSessionsController : VStoreController
    {
        private readonly LockSessionManager _lockSessionManager;

        public ManageLockSessionsController(LockSessionManager lockSessionManager)
        {
            _lockSessionManager = lockSessionManager;
        }

        [HttpGet]
        [ResponseCache(NoStore = true)]
        [ProducesResponseType(typeof(IReadOnlyCollection<long>), 200)]
        public async Task<IActionResult> ListAllCurrentLockSessios()
        {
            var keys = await _lockSessionManager.GetAllCurrentLockSessions();
            return Json(keys);
        }

        [HttpDelete("{rootObjectId}")]
        [ProducesResponseType(typeof(void), 202)]
        public async Task<IActionResult> DeleteSessionLock(long rootObjectId)
        {
            await _lockSessionManager.DeleteLockSession(rootObjectId);
            return Accepted();
        }
    }
}

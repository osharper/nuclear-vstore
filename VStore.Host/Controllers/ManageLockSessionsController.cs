using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Locks;

namespace NuClear.VStore.Host.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/{version:apiVersion}/mgmt/lock-sessions")]
    [Route("")]
    public sealed class ManageLockSessionsController : VStoreController
    {
        private readonly LockSessionManager _lockSessionManager;

        public ManageLockSessionsController(LockSessionManager lockSessionManager)
        {
            _lockSessionManager = lockSessionManager;
        }

        [HttpGet]
        public async Task<JsonResult> ListAllCurrentLockSessios()
        {
            var keys = await _lockSessionManager.GetAllCurrentLockSessions();
            return Json(keys);
        }

        [HttpDelete("{rootObjectId}")]
        public async Task DeleteSessionLock(string rootObjectId)
        {
            await _lockSessionManager.DeleteLockSession(rootObjectId);
        }
    }
}
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Locks;

namespace NuClear.VStore.Host.Controllers
{
    [Route("mgmt/lock-session")]
    public sealed class LockSessionManagementController : Controller
    {
        private readonly LockSessionManager _lockSessionManager;

        public LockSessionManagementController(LockSessionManager lockSessionManager)
        {
            _lockSessionManager = lockSessionManager;
        }

        [HttpGet]
        [Route("list")]
        public async Task<JsonResult> ListAllCurrentLockSessios()
        {
            var keys = await _lockSessionManager.GetAllCurrentLockSessions();
            return Json(keys);
        }

        [HttpDelete]
        [Route("{rootObjectId}")]
        public async Task DeleteSessionLock(string rootObjectId)
        {
            await _lockSessionManager.DeleteLockSession(rootObjectId);
        }
    }
}
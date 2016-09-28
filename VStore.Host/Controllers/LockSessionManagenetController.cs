using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Host.Locks;

namespace NuClear.VStore.Host.Controllers
{
    [Route("api/1.0/mgmt/lock-session")]
    public sealed class LockSessionManagenetController : Controller
    {
        private readonly LockSessionManager _lockSessionManager;

        public LockSessionManagenetController(LockSessionManager lockSessionManager)
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
        [Route("{rootObjectKey}")]
        public async Task DeleteSessionLock(string rootObjectKey)
        {
            await _lockSessionManager.DeleteLockSession(rootObjectKey);
        }
    }
}
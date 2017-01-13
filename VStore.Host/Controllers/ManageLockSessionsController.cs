using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Locks;
using NuClear.VStore.S3;

namespace NuClear.VStore.Host.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/{version:apiVersion}/mgmt/lock-sessions")]
    public sealed class ManageLockSessionsController : VStoreController
    {
        private readonly LockSessionManager _lockSessionManager;

        public ManageLockSessionsController(LockSessionManager lockSessionManager)
        {
            _lockSessionManager = lockSessionManager;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyCollection<string>), 200)]
        public async Task<JsonResult> ListAllCurrentLockSessios()
        {
            var keys = await _lockSessionManager.GetAllCurrentLockSessions();
            return Json(keys);
        }

        [HttpDelete("{rootObjectId}")]
        [ProducesResponseType(typeof(void), 202)]
        public async Task<IActionResult> DeleteSessionLock(string rootObjectId)
        {
            try
            {
                await _lockSessionManager.DeleteLockSession(rootObjectId);
                return Accepted();
            }
            catch (S3Exception)
            {
                return InternalServerError();
            }
        }
    }
}
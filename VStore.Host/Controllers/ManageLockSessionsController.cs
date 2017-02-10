using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Locks;

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
        [ResponseCache(NoStore = true)]
        [ProducesResponseType(typeof(IReadOnlyCollection<string>), 200)]
        public async Task<IActionResult> ListAllCurrentLockSessios()
        {
            try
            {
                var keys = await _lockSessionManager.GetAllCurrentLockSessions();
                return Json(keys);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex, "Error occured while listing session locks");
            }
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
            catch (Exception ex)
            {
                return InternalServerError(ex, "Error occured while deleting session lock");
            }
        }
    }
}
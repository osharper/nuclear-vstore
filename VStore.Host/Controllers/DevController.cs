using System;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Host.Locks;

namespace NuClear.VStore.Host.Controllers
{
    [Route("dev")]
    public sealed class DevController : Controller
    {
        private readonly LockSessionFactory _lockSessionFactory;

        public DevController(LockSessionFactory lockSessionFactory)
        {
            _lockSessionFactory = lockSessionFactory;
        }

        [HttpPut]
        [Route("lock-session")]
        public void CreateLockSession()
        {
            _lockSessionFactory.CreateLockSession(Guid.Empty.ToString());
        }
    }
}
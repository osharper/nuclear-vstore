using System;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using NuClear.VStore.Locks;
using NuClear.VStore.Options;

namespace NuClear.VStore.Host.Controllers
{
    [Route("api/{version:apiVersion}/dev")]
    public sealed class DevController : VStoreController
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly IOptions<CephOptions> _cephOptions;
        private readonly LockSessionFactory _lockSessionFactory;

        public DevController(IAmazonS3 amazonS3, IOptions<CephOptions> cephOptions, LockSessionFactory lockSessionFactory)
        {
            _amazonS3 = amazonS3;
            _cephOptions = cephOptions;
            _lockSessionFactory = lockSessionFactory;
        }

        [HttpPut("lock-session")]
        public void CreateLockSession()
        {
            _lockSessionFactory.CreateLockSession(0);
        }

        [HttpGet("object")]
        public async Task<JsonResult> List()
        {
            var response = await _amazonS3.ListVersionsAsync(_cephOptions.Value.ObjectsBucketName);
            return Json(response.Versions.Where(x => !x.IsDeleteMarker).Select(x => new { x.Key, x.VersionId, x.IsLatest }));
        }

        [HttpGet("throw")]
        public void Throw()
        {
            throw new Exception("Test exception");
        }
    }
}

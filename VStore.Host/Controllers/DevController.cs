using System;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using NuClear.VStore.Locks;
using NuClear.VStore.Options;

namespace NuClear.VStore.Host.Controllers
{
    [Route("dev")]
    public sealed class DevController : Controller
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

        [HttpGet("content")]
        public async Task<JsonResult> List()
        {
            var response = await _amazonS3.ListVersionsAsync(_cephOptions.Value.ContentBucketName);
            return Json(response.Versions.Where(x => !x.IsDeleteMarker).Select(x => new { x.Key, x.VersionId, x.IsLatest }));
        }

        [HttpGet("content/binary/{id}/{versionId}")]
        public async Task<FileStreamResult> GetBinary(string id, string versionId)
        {
            var response = await _amazonS3.GetObjectAsync(_cephOptions.Value.ContentBucketName, id, versionId);
            return File(response.ResponseStream, response.Headers.ContentType);
        }

        [HttpPut("content/{id}")]
        public async Task<string> Put(string id, IFormFile file)
        {
            var response = await _amazonS3.PutObjectAsync(
                               new PutObjectRequest
                                   {
                                       Key = id,
                                       BucketName = _cephOptions.Value.ContentBucketName,
                                       ContentType = file.ContentType,
                                       InputStream = file.OpenReadStream(),
                                       CannedACL = S3CannedACL.PublicRead
                                   });
            return response.VersionId;
        }
    }
}
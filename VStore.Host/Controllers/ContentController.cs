using System.Linq;
using System.Threading.Tasks;

using Amazon.Extensions.NETCore.Setup;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using NuClear.VStore.Host.Options;

namespace NuClear.VStore.Host.Controllers
{
    [Route("api/1.0/content")]
    public sealed class ContentController : Controller
    {
        private readonly string _bucketName;
        private readonly AmazonS3Client _amazonS3Client;

        public ContentController(AWSOptions awsOptions, IOptions<CephOptions> cephOptions)
        {
            _amazonS3Client = new AmazonS3Client(
                                  new StoredProfileAWSCredentials(awsOptions.Profile),
                                  new AmazonS3Config { ServiceURL = awsOptions.DefaultClientConfig.ServiceURL });
            _bucketName = cephOptions.Value.BucketName;
        }

        [HttpGet]
        [Route("list")]
        public async Task<JsonResult> List()
        {
            var response = await _amazonS3Client.ListVersionsAsync(_bucketName);
            return Json(response.Versions.Where(x => !x.IsDeleteMarker).Select(x => new { x.Key, x.VersionId, x.IsLatest }));
        }

        [HttpGet]
        [Route("{key}/{version}")]
        public async Task<FileStreamResult> Get(string key, string version)
        {
            var response = await _amazonS3Client.GetObjectAsync(_bucketName, key, version);
            return File(response.ResponseStream, response.Headers.ContentType);
        }

        [HttpPut]
        [Route("{key}")]
        public async Task<string> Put(string key, IFormFile file)
        {
            var response = await _amazonS3Client.PutObjectAsync(
                               new PutObjectRequest
                                   {
                                       Key = key,
                                       BucketName = _bucketName,
                                       ContentType = file.ContentType,
                                       InputStream = file.OpenReadStream(),
                                       CannedACL = S3CannedACL.PublicRead
                                   });
            return response.VersionId;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _amazonS3Client.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
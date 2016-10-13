using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using NuClear.VStore.Content;
using NuClear.VStore.Descriptors;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.Options;

namespace NuClear.VStore.Host.Controllers
{
    [Route("content")]
    public sealed class ContentController : Controller
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly ContentStorageReader _contentStorageReader;
        private readonly ContentManagementService _contentManagementService;
        private readonly string _bucketName;

        public ContentController(
            IAmazonS3 amazonS3,
            IOptions<CephOptions> cephOptions,
            ContentStorageReader contentStorageReader,
            ContentManagementService contentManagementService)
        {
            _amazonS3 = amazonS3;
            _contentStorageReader = contentStorageReader;
            _contentManagementService = contentManagementService;
            _bucketName = cephOptions.Value.ContentBucketName;
        }

        [HttpGet]
        public async Task<JsonResult> List()
        {
            var response = await _amazonS3.ListVersionsAsync(_bucketName);
            return Json(response.Versions.Where(x => !x.IsDeleteMarker).Select(x => new { x.Key, x.VersionId, x.IsLatest }));
        }

        [HttpGet("{id}/{versionId}")]
        public async Task<FileStreamResult> Get(string id, string versionId)
        {
            var response = await _amazonS3.GetObjectAsync(_bucketName, id, versionId);
            return File(response.ResponseStream, response.Headers.ContentType);
        }

        [HttpPut("{id}/{elementId}")]
        public async Task<string> Put(string id, string elementId, IFormFile file)
        {
            var response = await _amazonS3.PutObjectAsync(
                               new PutObjectRequest
                                   {
                                       Key = id,
                                       BucketName = _bucketName,
                                       ContentType = file.ContentType,
                                       InputStream = file.OpenReadStream(),
                                       CannedACL = S3CannedACL.PublicRead
                                   });
            return response.VersionId;
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Initialize(long id, [FromBody]IVersionedTemplateDescriptor templateDescriptor)
        {
            var versionId = await _contentManagementService.Initialize(id, templateDescriptor);
            var url = Url.AbsoluteAction("Get", "Content", new { id, versionId });
            return Created(url, versionId);
        }

        [HttpGet("template/{id}/{versionId}")]
        public async Task<JsonResult> GetTemplateDescriptor(long id, string versionId)
        {
            var descriptor = await _contentStorageReader.GetTemplateDescriptor(id, versionId);
            return Json(descriptor);
        }
    }
}
using System.Collections.Concurrent;
using System.Threading.Tasks;

using Amazon.S3;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using NuClear.VStore.Host.Core;
using NuClear.VStore.Host.Model;
using NuClear.VStore.Host.Options;

namespace NuClear.VStore.Host.Controllers
{
    [Route("api/1.0/templates")]
    public class TemplatesController : Controller
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName;

        public TemplatesController(IAmazonS3 amazonS3, IOptions<CephOptions> cephOptions)
        {
            _amazonS3 = amazonS3;
            _bucketName = cephOptions.Value.TemplatesBucketName;
        }

        [HttpGet]
        public async Task<JsonResult> List()
        {
            var listObjectsResponse = await _amazonS3.ListObjectsAsync(_bucketName);

            var descriptors = new ConcurrentBag<TemplateDescriptor>();
            Parallel.ForEach(
                listObjectsResponse.S3Objects,
                async obj =>
                    {
                        var metadataResponse = await _amazonS3.GetObjectMetadataAsync(_bucketName, obj.Key);
                        descriptors.Add(new TemplateDescriptor(obj.Key, metadataResponse.VersionId, metadataResponse.Metadata["name".AsMetadata()]));
                    });

            return Json(descriptors);
        }
    }
}
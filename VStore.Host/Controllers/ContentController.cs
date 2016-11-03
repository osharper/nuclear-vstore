using System;
using System.Threading.Tasks;

using Amazon.S3;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.Objects;
using NuClear.VStore.Options;
using NuClear.VStore.S3;

namespace NuClear.VStore.Host.Controllers
{
    [Route("content")]
    public sealed class ContentController : Controller
    {
        private readonly ContentStorageReader _contentStorageReader;
        private readonly ContentManagementService _contentManagementService;

        public ContentController(
            IAmazonS3 amazonS3,
            IOptions<CephOptions> cephOptions,
            ContentStorageReader contentStorageReader,
            ContentManagementService contentManagementService)
        {
            _contentStorageReader = contentStorageReader;
            _contentManagementService = contentManagementService;
        }

        [HttpGet("template/{id}/{versionId}")]
        public async Task<JsonResult> GetTemplateDescriptor(long id, string versionId)
        {
            var descriptor = await _contentStorageReader.GetTemplateDescriptor(id, versionId);
            return Json(descriptor);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(long id)
        {
            try
            {
                var descriptor = await _contentStorageReader.GetContentDescriptor(id, null);
                return Json(descriptor);
            }
            catch (ObjectNotFoundException)
            {
                return NotFound(id);
            }
            catch (ObjectInconsistentException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id}/{versionId}")]
        public async Task<IActionResult> Get(long id, string versionId)
        {
            try
            {
                var descriptor = await _contentStorageReader.GetContentDescriptor(id, versionId);
                return Json(descriptor);
            }
            catch (ObjectNotFoundException)
            {
                return NotFound(id);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id}/{templateId}")]
        public async Task<IActionResult> Create(long id, long templateId, [FromBody]IVersionedTemplateDescriptor templateDescriptor)
        {
            var versionId = await _contentManagementService.Create(id, templateId, templateDescriptor);
            var url = Url.AbsoluteAction("Get", "Content", new { id, versionId });
            return Created(url, versionId);
        }
    }
}
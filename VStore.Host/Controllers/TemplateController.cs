using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Host.Controllers
{
    [Route("template")]
    public class TemplateController : Controller
    {
        private readonly TemplateStorageReader _templateStorageReader;
        private readonly TemplateManagementService _templateManagementService;

        public TemplateController(TemplateStorageReader templateStorageReader, TemplateManagementService templateManagementService)
        {
            _templateStorageReader = templateStorageReader;
            _templateManagementService = templateManagementService;
        }

        [HttpGet("element-descriptors/available")]
        public JsonResult GetAvailableElementDescriptors()
        {
            return Json(_templateManagementService.GetAvailableElementDescriptors());
        }

        [HttpGet]
        public async Task<JsonResult> List()
        {
            return Json(await _templateStorageReader.GetAllTemplateDescriptors());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            try
            {
                return Json(await _templateStorageReader.GetTemplateDescriptor(id, null));
            }
            catch (ObjectNotFoundException)
            {
                return NotFound(id);
            }
        }

        [HttpGet("{id}/{versionId}")]
        public async Task<IActionResult> Get(Guid id, string versionId)
        {
            try
            {
                return Json(await _templateStorageReader.GetTemplateDescriptor(id, versionId));
            }
            catch (ObjectNotFoundException)
            {
                return NotFound(new { id, versionId });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateTemplate([FromBody] ITemplateDescriptor templateDescriptor)
        {
            try
            {
                var versionId = await _templateManagementService.CreateTemplate(templateDescriptor);
                var url = Url.AbsoluteAction("Get", "Template", new { templateDescriptor.Id, versionId });
                return Created(url, null);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ex.Message });
            }
        }

        [HttpPut]
        public async Task<IActionResult> ModifyTemplate([FromBody] IVersionedTemplateDescriptor templateDescriptor)
        {
            try
            {
                var versionId = await _templateManagementService.ModifyTemplate(templateDescriptor);
                return Ok(versionId);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ex.Message });
            }
        }
    }
}
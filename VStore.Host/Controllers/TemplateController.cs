using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Host.Core;
using NuClear.VStore.Host.Descriptors;
using NuClear.VStore.Host.Templates;

namespace NuClear.VStore.Host.Controllers
{
    [Route("template")]
    public class TemplateController : Controller
    {
        private readonly TemplateManagementService _templateManagementService;

        public TemplateController(TemplateManagementService templateManagementService)
        {
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
            return Json(await _templateManagementService.GetAllTemplateDescriptors());
        }

        [HttpGet("{id}")]
        public async Task<JsonResult> Get(Guid id)
        {
            return Json(await _templateManagementService.GetTemplateDescriptor(id, null));
        }

        [HttpGet("{id}/{versionId}")]
        public async Task<JsonResult> Get(Guid id, string versionId)
        {
            return Json(await _templateManagementService.GetTemplateDescriptor(id, versionId));
        }

        [HttpPut]
        public async Task<IActionResult> CreateTemplate([FromBody] TemplateDescriptor templateDescriptor)
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

        [HttpPost]
        public async Task<IActionResult> ModifyTemplate([FromBody] TemplateDescriptor templateDescriptor)
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
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Host.Descriptors;
using NuClear.VStore.Host.Templates;

namespace NuClear.VStore.Host.Controllers
{
    [Route("template")]
    public class TemplatesController : Controller
    {
        private readonly TemplateManagementService _templateManagementService;

        public TemplatesController(TemplateManagementService templateManagementService)
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

        [HttpPut]
        public async Task<IActionResult> CreateTemplate([FromBody]TemplateDescriptor templateDescriptor)
        {
            await _templateManagementService.CreateTemplate(templateDescriptor);
            return Ok();
        }
    }
}
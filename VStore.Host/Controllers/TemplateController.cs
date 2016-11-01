using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Descriptors.Templates;
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
            return Json(await _templateStorageReader.GetTemplateMetadatas());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(long id)
        {
            try
            {
                var templateDescriptor = await _templateStorageReader.GetTemplateDescriptor(id, null);
                return Json(new { Template = templateDescriptor, templateDescriptor.VersionId });
            }
            catch (ObjectNotFoundException)
            {
                return NotFound(id);
            }
        }

        [HttpGet("{id}/{versionId}")]
        public async Task<IActionResult> Get(long id, string versionId)
        {
            try
            {
                var templateDescriptor = await _templateStorageReader.GetTemplateDescriptor(id, versionId);
                return Json(new { Template = templateDescriptor, templateDescriptor.VersionId });
            }
            catch (ObjectNotFoundException)
            {
                return NotFound(new { id, versionId });
            }
        }
        [HttpPost("validate-elements")]
        public IActionResult CreateTemplate([FromBody] IReadOnlyCollection<IElementDescriptor> elementDescriptors)
        {
            try
            {
                _templateManagementService.VerifyElementDescriptorsConsistency(null, elementDescriptors);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { ex.Message });
            }
        }

        [HttpPost("validate-elements/{id}")]
        public IActionResult CreateTemplate(long id, [FromBody] IReadOnlyCollection<IElementDescriptor> elementDescriptors)
        {
            try
            {
                _templateManagementService.VerifyElementDescriptorsConsistency(id, elementDescriptors);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { ex.Message });
            }
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> CreateTemplate(long id, [FromBody] ITemplateDescriptor templateDescriptor)
        {
            try
            {
                var versionId = await _templateManagementService.CreateTemplate(id, templateDescriptor);
                var url = Url.AbsoluteAction("Get", "Template", new { id, versionId });
                return Created(url, versionId);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ex.Message });
            }
        }

        [HttpPut("{id}/{versionId}")]
        public async Task<IActionResult> ModifyTemplate(long id, string versionId, [FromBody] ITemplateDescriptor templateDescriptor)
        {
            try
            {
                var latestVersionId = await _templateManagementService.ModifyTemplate(id, versionId, templateDescriptor);
                return Ok(latestVersionId);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ex.Message });
            }
        }
    }
}
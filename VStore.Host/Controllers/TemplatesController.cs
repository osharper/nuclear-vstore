using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.Locks;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Host.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/{version:apiVersion}/templates")]
    public class TemplatesController : VStoreController
    {
        private readonly TemplatesStorageReader _templatesStorageReader;
        private readonly TemplatesManagementService _templatesManagementService;

        public TemplatesController(TemplatesStorageReader templatesStorageReader, TemplatesManagementService templatesManagementService)
        {
            _templatesStorageReader = templatesStorageReader;
            _templatesManagementService = templatesManagementService;
        }

        [HttpGet("element-descriptors/available")]
        public JsonResult GetAvailableElementDescriptors()
        {
            return Json(_templatesManagementService.GetAvailableElementDescriptors());
        }

        [HttpGet]
        public async Task<JsonResult> List()
        {
            return Json(await _templatesStorageReader.GetTemplateMetadatas());
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(long id)
        {
            try
            {
                var templateDescriptor = await _templatesStorageReader.GetTemplateDescriptor(id, null);
                return Json(
                    new
                        {
                            id,
                            templateDescriptor.VersionId,
                            templateDescriptor.LastModified,
                            templateDescriptor.Properties,
                            templateDescriptor.Elements
                        });
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
                var templateDescriptor = await _templatesStorageReader.GetTemplateDescriptor(id, versionId);
                return Json(
                    new
                    {
                        id,
                        templateDescriptor.VersionId,
                        templateDescriptor.LastModified,
                        templateDescriptor.Properties,
                        templateDescriptor.Elements
                    });
            }
            catch (ObjectNotFoundException)
            {
                return NotFound(new { id, versionId });
            }
        }

        [HttpPost("validate-elements")]
        public IActionResult ValidateElements([FromBody] IReadOnlyCollection<IElementDescriptor> elementDescriptors)
        {
            try
            {
                _templatesManagementService.VerifyElementDescriptorsConsistency(null, elementDescriptors);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { ex.Message });
            }
        }

        [HttpPost("{id}/validate-elements")]
        public IActionResult ValidateElements(long id, [FromBody] IReadOnlyCollection<IElementDescriptor> elementDescriptors)
        {
            try
            {
                _templatesManagementService.VerifyElementDescriptorsConsistency(id, elementDescriptors);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(new { ex.Message });
            }
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> Create(long id, [FromBody] ITemplateDescriptor templateDescriptor)
        {
            try
            {
                var versionId = await _templatesManagementService.CreateTemplate(id, templateDescriptor);
                var url = Url.AbsoluteAction("Get", "Template", new { id, versionId });
                return Created(url, versionId);
            }
            catch (Exception ex)
            {
                return BadRequest(new { ex.Message });
            }
        }

        [HttpPut("{id}/{versionId}")]
        public async Task<IActionResult> Modify(long id, string versionId, [FromBody] ITemplateDescriptor templateDescriptor)
        {
            try
            {
                var latestVersionId = await _templatesManagementService.ModifyTemplate(id, versionId, templateDescriptor);
                return Ok(latestVersionId);
            }
            catch (SessionLockAlreadyExistsException)
            {
                return Conflict();
            }
            catch (Exception ex)
            {
                return BadRequest(new { ex.Message });
            }
        }
    }
}

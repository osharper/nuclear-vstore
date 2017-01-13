using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Descriptors;
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
        [ProducesResponseType(typeof(IReadOnlyCollection<IElementDescriptor>), 200)]
        public JsonResult GetAvailableElementDescriptors()
        {
            return Json(_templatesManagementService.GetAvailableElementDescriptors());
        }

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyCollection<ImmutableDescriptor>), 200)]
        public async Task<JsonResult> List()
        {
            return Json(await _templatesStorageReader.GetTemplateMetadatas());
        }

        [HttpGet("{id}")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(404)]
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
                return NotFound();
            }
        }

        [HttpGet("{id}/{versionId}")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(404)]
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
                return NotFound();
            }
        }

        [HttpPost("validate-elements")]
        [ProducesResponseType(200)]
        [ProducesResponseType(typeof(object), 400)]
        public IActionResult ValidateElements([FromBody] IReadOnlyCollection<IElementDescriptor> elementDescriptors)
        {
            try
            {
                _templatesManagementService.VerifyElementDescriptorsConsistency(null, elementDescriptors);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }

        [HttpPost("{id}/validate-elements")]
        [ProducesResponseType(200)]
        [ProducesResponseType(typeof(object), 400)]
        public IActionResult ValidateElements(long id, [FromBody] IReadOnlyCollection<IElementDescriptor> elementDescriptors)
        {
            try
            {
                _templatesManagementService.VerifyElementDescriptorsConsistency(id, elementDescriptors);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }

        [HttpPost("{id}")]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(object), 400)]
        public async Task<IActionResult> Create(long id, [FromBody] ITemplateDescriptor templateDescriptor)
        {
            try
            {
                var versionId = await _templatesManagementService.CreateTemplate(id, templateDescriptor);
                var url = Url.AbsoluteAction("Get", "Templates", new { id, versionId });
                return Created(url, null);
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }

        [HttpPut("{id}/{versionId}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> Modify(long id, string versionId, [FromBody] ITemplateDescriptor templateDescriptor)
        {
            try
            {
                var latestVersionId = await _templatesManagementService.ModifyTemplate(id, versionId, templateDescriptor);
                var url = Url.AbsoluteAction("Get", "Templates", new { id, versionId = latestVersionId });
                return NoContent(url);
            }
            catch (SessionLockAlreadyExistsException)
            {
                return Conflict();
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }
    }
}

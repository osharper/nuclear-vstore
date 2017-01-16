using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.Locks;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

using Swashbuckle.AspNetCore.Swagger;

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
        [ProducesResponseType(304)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Get(
            [FromHeader(Name = HeaderNames.IfNoneMatch)] string ifNoneMatch,
            long id)
        {
            try
            {
                var templateDescriptor = await _templatesStorageReader.GetTemplateDescriptor(id, null);
                SetResponseHeaders(templateDescriptor);

                if (ifNoneMatch == templateDescriptor.VersionId)
                {
                    return NotModified();
                }

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
        [ProducesResponseType(304)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Get(long id, string versionId)
        {
            try
            {
                var templateDescriptor = await _templatesStorageReader.GetTemplateDescriptor(id, versionId);
                SetResponseHeaders(templateDescriptor);

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

        [HttpPut("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(409)]
        [ProducesResponseType(412)]
        public async Task<IActionResult> Modify(
            [FromHeader(Name = HeaderNames.IfMatch)] string ifMatch,
            [FromBody] ITemplateDescriptor templateDescriptor,
            long id)
        {
            try
            {
                if (string.IsNullOrEmpty(ifMatch))
                {
                    Response.ContentType = ContentType.PlainText;
                    return BadRequest($"'{HeaderNames.IfMatch}' request header must be specified.");
                }

                var latestVersionId = await _templatesManagementService.ModifyTemplate(id, ifMatch, templateDescriptor);
                var url = Url.AbsoluteAction("Get", "Templates", new { id, versionId = latestVersionId });
                return NoContent(url);
            }
            catch (SessionLockAlreadyExistsException)
            {
                return Conflict();
            }
            catch (ConcurrencyException)
            {
                return PreconditionFailed();
            }
            catch (Exception ex)
            {
                return BadRequest(ex);
            }
        }
    }
}

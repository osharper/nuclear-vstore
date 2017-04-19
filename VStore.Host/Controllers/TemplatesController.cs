using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.Locks;
using NuClear.VStore.Objects;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Host.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/{version:apiVersion}/templates")]
    public class TemplatesController : VStoreController
    {
        private readonly ILogger<TemplatesController> _logger;
        private readonly TemplatesStorageReader _templatesStorageReader;
        private readonly TemplatesManagementService _templatesManagementService;

        public TemplatesController(TemplatesStorageReader templatesStorageReader, TemplatesManagementService templatesManagementService, ILogger<TemplatesController> logger)
        {
            _logger = logger;
            _templatesStorageReader = templatesStorageReader;
            _templatesManagementService = templatesManagementService;
        }

        [HttpGet("element-descriptors/available")]
        [ProducesResponseType(typeof(IReadOnlyCollection<IElementDescriptor>), 200)]
        public IActionResult GetAvailableElementDescriptors()
        {
            try
            {
                return Json(_templatesManagementService.GetAvailableElementDescriptors());
            }
            catch (Exception ex)
            {
                return InternalServerError(ex, "Unexpected error while getting available element descriptors");
            }
        }

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyCollection<IdentifyableObjectDescriptor<long>>), 200)]
        public async Task<IActionResult> List([FromHeader(Name = Headers.HeaderNames.AmsContinuationToken)]string continuationToken)
        {
            try
            {
                var container = await _templatesStorageReader.GetTemplateMetadatas(continuationToken?.Trim('"'));

                if (!string.IsNullOrEmpty(container.ContinuationToken))
                {
                    Response.Headers[Headers.HeaderNames.AmsContinuationToken] = $"\"{container.ContinuationToken}\"";
                }

                return Json(container.Collection);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex, "Unexpected error while listing templates");
            }
        }

        [HttpGet("{id}")]
        [ResponseCache(Duration = 120)]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(304)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Get(long id, [FromHeader(Name = HeaderNames.IfNoneMatch)] string ifNoneMatch)
        {
            try
            {
                var templateDescriptor = await _templatesStorageReader.GetTemplateDescriptor(id, null);

                Response.Headers[HeaderNames.ETag] = $"\"{templateDescriptor.VersionId}\"";
                Response.Headers[HeaderNames.LastModified] = templateDescriptor.LastModified.ToString("R");

                if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch.Trim('"') == templateDescriptor.VersionId)
                {
                    return NotModified();
                }

                return Json(
                    new
                        {
                            id,
                            templateDescriptor.VersionId,
                            templateDescriptor.LastModified,
                            templateDescriptor.Author,
                            templateDescriptor.Properties,
                            templateDescriptor.Elements
                        });
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                return InternalServerError(ex, "Unexpected error while getting template with id '{id}'", id);
            }
        }

        [HttpGet("{id}/{versionId}")]
        [ResponseCache(Duration = 120)]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetVersion(long id, string versionId)
        {
            try
            {
                var templateDescriptor = await _templatesStorageReader.GetTemplateDescriptor(id, versionId);

                Response.Headers[HeaderNames.ETag] = $"\"{templateDescriptor.VersionId}\"";
                Response.Headers[HeaderNames.LastModified] = templateDescriptor.LastModified.ToString("R");
                return Json(
                    new
                        {
                            id,
                            templateDescriptor.VersionId,
                            templateDescriptor.LastModified,
                            templateDescriptor.Author,
                            templateDescriptor.Properties,
                            templateDescriptor.Elements
                        });
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                return InternalServerError(ex, "Unexpected error while getting template with id '{id}' and versionId '{versionId}'", id, versionId);
            }
        }

        [HttpPost("validate-elements")]
        [ProducesResponseType(200)]
        [ProducesResponseType(typeof(object), 422)]
        public IActionResult ValidateElements([FromBody] IReadOnlyCollection<IElementDescriptor> elementDescriptors)
        {
            try
            {
                _templatesManagementService.VerifyElementDescriptorsConsistency(elementDescriptors);
                return Ok();
            }
            catch (AggregateException ex)
            {
                return Unprocessable(GenerateTemplateErrorJson(ex));
            }
            catch (Exception ex)
            {
                return InternalServerError(ex, "Unexpected error while getting template elements validation");
            }
        }

        [HttpPost("{id}/validate-elements")]
        [ProducesResponseType(200)]
        [ProducesResponseType(typeof(object), 422)]
        public IActionResult ValidateElements(long id, [FromBody] IReadOnlyCollection<IElementDescriptor> elementDescriptors)
        {
            try
            {
                _templatesManagementService.VerifyElementDescriptorsConsistency(elementDescriptors);
                return Ok();
            }
            catch (AggregateException ex)
            {
                return Unprocessable(GenerateTemplateErrorJson(ex));
            }
            catch (Exception ex)
            {
                return InternalServerError(ex, "Unexpected error while getting template elements validation for template with id '{id}'", id);
            }
        }

        [HttpPost("{id}")]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(409)]
        [ProducesResponseType(typeof(object), 422)]
        public async Task<IActionResult> Create(
            long id,
            [FromHeader(Name = Headers.HeaderNames.AmsAuthor)] string author,
            [FromBody] ITemplateDescriptor templateDescriptor)
        {
            if (string.IsNullOrEmpty(author))
            {
                return BadRequest($"'{Headers.HeaderNames.AmsAuthor}' request header must be specified.");
            }

            if (templateDescriptor == null)
            {
                return BadRequest("Template descriptor must be set.");
            }

            try
            {
                var versionId = await _templatesManagementService.CreateTemplate(id, author, templateDescriptor);
                var url = Url.AbsoluteAction("GetVersion", "Templates", new { id, versionId });

                Response.Headers[HeaderNames.ETag] = $"\"{versionId}\"";
                return Created(url, null);
            }
            catch (ObjectAlreadyExistsException)
            {
                return Conflict();
            }
            catch (AggregateException ex)
            {
                return Unprocessable(GenerateTemplateErrorJson(ex));
            }
            catch (Exception ex)
            {
                return InternalServerError(ex, "Unexpected error while template creation with id '{id}'", id);
            }
        }

        [HttpPut("{id}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(412)]
        [ProducesResponseType(typeof(object), 422)]
        public async Task<IActionResult> Modify(
            long id,
            [FromHeader(Name = HeaderNames.IfMatch)] string ifMatch,
            [FromHeader(Name = Headers.HeaderNames.AmsAuthor)] string author,
            [FromBody] ITemplateDescriptor templateDescriptor)
        {
            if (string.IsNullOrEmpty(ifMatch))
            {
                return BadRequest($"'{HeaderNames.IfMatch}' request header must be specified.");
            }

            if (string.IsNullOrEmpty(author))
            {
                return BadRequest($"'{Headers.HeaderNames.AmsAuthor}' request header must be specified.");
            }

            if (templateDescriptor == null)
            {
                return BadRequest("Template descriptor must be set.");
            }

            try
            {
                var latestVersionId = await _templatesManagementService.ModifyTemplate(id, ifMatch.Trim('"'), author, templateDescriptor);
                var url = Url.AbsoluteAction("GetVersion", "Templates", new { id, versionId = latestVersionId });

                Response.Headers[HeaderNames.ETag] = $"\"{latestVersionId}\"";
                return NoContent(url);
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
            }
            catch (AggregateException ex)
            {
                return Unprocessable(GenerateTemplateErrorJson(ex));
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
                return InternalServerError(ex, "Unexpected error while template modification with id '{id}'", id);
            }
        }

        private JToken GenerateTemplateErrorJson(AggregateException ex)
        {
            var errors = new JArray();
            ex.Handle(exception =>
            {
                var templateValidationException = exception as TemplateValidationException;
                if (templateValidationException != null)
                {
                    errors.Add(templateValidationException.SerializeToJson());
                    return true;
                }

                _logger.LogError(new EventId(), exception, "Unknown exception in generating validation errors JSON");
                return false;
            });

            return errors;
        }
    }
}

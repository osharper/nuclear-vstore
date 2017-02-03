using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.Json;
using NuClear.VStore.Locks;
using NuClear.VStore.Objects;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.S3;

namespace NuClear.VStore.Host.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/{version:apiVersion}/objects")]
    public sealed class ObjectsController : VStoreController
    {
        private readonly ObjectsStorageReader _objectsStorageReader;
        private readonly ObjectsManagementService _objectsManagementService;
        private readonly ILogger<ObjectsController> _logger;

        public ObjectsController(ObjectsStorageReader objectsStorageReader, ObjectsManagementService objectsManagementService, ILogger<ObjectsController> logger)
        {
            _logger = logger;
            _objectsStorageReader = objectsStorageReader;
            _objectsManagementService = objectsManagementService;
        }

        [HttpGet("{id}/{versionId}/template")]
        [ResponseCache(Duration = 120)]
        [ProducesResponseType(typeof(IVersionedTemplateDescriptor), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetTemplateDescriptor(long id, string versionId)
        {
            try
            {
                var templateDescriptor = await _objectsStorageReader.GetTemplateDescriptor(id, versionId);

                Response.Headers[HeaderNames.ETag] = $"\"{templateDescriptor.VersionId}\"";
                Response.Headers[HeaderNames.LastModified] = templateDescriptor.LastModified.ToString("R");
                return Json(templateDescriptor);
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                return InternalServerError(ex, "Unexpected error while getting template for the object with id '{id}' and versionId {versionId}", id, versionId);
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
                var objectDescriptor = await _objectsStorageReader.GetObjectDescriptor(id, null);

                Response.Headers[HeaderNames.ETag] = $"\"{objectDescriptor.VersionId}\"";
                Response.Headers[HeaderNames.LastModified] = objectDescriptor.LastModified.ToString("R");

                if (!string.IsNullOrEmpty(ifNoneMatch) && ifNoneMatch.Trim('"') == objectDescriptor.VersionId)
                {
                    return NotModified();
                }

                return Json(
                    new
                    {
                        objectDescriptor.Id,
                        objectDescriptor.VersionId,
                        objectDescriptor.LastModified,
                        objectDescriptor.TemplateId,
                        objectDescriptor.TemplateVersionId,
                        objectDescriptor.Language,
                        objectDescriptor.Author,
                        objectDescriptor.Properties,
                        objectDescriptor.Elements
                    });
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
            }
            catch (Exception ex)
            {
                return InternalServerError(ex, "Error occured while getting the object with id '{id}'", id);
            }
        }

        [HttpGet("{id}/{versionId}")]
        [ResponseCache(Duration = 120)]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetVersion(long id, string versionId)
        {
            try
            {
                var objectDescriptor = await _objectsStorageReader.GetObjectDescriptor(id, versionId);

                Response.Headers[HeaderNames.ETag] = $"\"{objectDescriptor.VersionId}\"";
                Response.Headers[HeaderNames.LastModified] = objectDescriptor.LastModified.ToString("R");
                return Json(
                    new
                    {
                        objectDescriptor.Id,
                        objectDescriptor.VersionId,
                        objectDescriptor.LastModified,
                        objectDescriptor.TemplateId,
                        objectDescriptor.TemplateVersionId,
                        objectDescriptor.Language,
                        objectDescriptor.Author,
                        objectDescriptor.Properties,
                        objectDescriptor.Elements
                    });
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
            }
            catch (ObjectInconsistentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex, "Error occured while getting the object with id '{id}' and versionId {versionId}", id, versionId);
            }
        }

        [HttpPost("{id}")]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(typeof(object), 422)]
        public async Task<IActionResult> Create(
            long id,
            [FromHeader(Name = Headers.HeaderNames.AmsAuthor)] string author,
            [FromBody] IObjectDescriptor objectDescriptor)
        {
            if (string.IsNullOrEmpty(author))
            {
                return BadRequest($"'{Headers.HeaderNames.AmsAuthor}' request header must be specified.");
            }

            if (objectDescriptor == null)
            {
                return BadRequest("Object descriptor must be set.");
            }

            try
            {
                var versionId = await _objectsManagementService.Create(id, author, objectDescriptor);
                var url = Url.AbsoluteAction("GetVersion", "Objects", new { id, versionId });

                Response.Headers[HeaderNames.ETag] = $"\"{versionId}\"";
                return Created(url, null);
            }
            catch (AggregateException ex)
            {
                return Unprocessable(GenerateErrorJsonResult(ex));
            }
            catch (ObjectNotFoundException ex)
            {
                _logger.LogError(new EventId(0), ex, "Error occured while creating object");

                Response.ContentType = ContentType.PlainText;
                return Unprocessable(ex.Message);
            }
            catch (ObjectAlreadyExistsException)
            {
                return Conflict();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(new EventId(0), ex, "Error occured while creating object");
                return BadRequest(ex.Message);
            }
            catch (ObjectInconsistentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex, "Unknown error occured while creating object with id '{id}'", id);
            }
        }

        [HttpPatch("{id}")]
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
            [FromBody] IObjectDescriptor objectDescriptor)
        {
            ifMatch = ifMatch.Trim('"');
            if (string.IsNullOrEmpty(ifMatch))
            {
                return BadRequest($"'{HeaderNames.IfMatch}' request header must be specified.");
            }

            if (string.IsNullOrEmpty(author))
            {
                return BadRequest($"'{Headers.HeaderNames.AmsAuthor}' request header must be specified.");
            }

            if (objectDescriptor == null)
            {
                return BadRequest("Object descriptor must be set.");
            }

            try
            {
                var latestVersionId = await _objectsManagementService.Modify(id, ifMatch, author, objectDescriptor);
                var url = Url.AbsoluteAction("GetVersion", "Objects", new { id, versionId = latestVersionId });

                Response.Headers[HeaderNames.ETag] = $"\"{latestVersionId}\"";
                return NoContent(url);
            }
            catch (AggregateException ex)
            {
                return Unprocessable(GenerateErrorJsonResult(ex));
            }
            catch (ObjectNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (SessionLockAlreadyExistsException)
            {
                return Conflict();
            }
            catch (ConcurrencyException)
            {
                return PreconditionFailed();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(new EventId(), ex, "Error occured while modifying object");
                return BadRequest(ex.Message);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (ObjectInconsistentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex, "Unknown error occured while modifying the object with id '{id}' and versionId {ifMatch}", id, ifMatch);
            }
        }

        private static JToken GenerateErrorJsonResult(AggregateException ex)
        {
            var content = new JArray();
            foreach (var exception in ex.InnerExceptions)
            {
                var invalidObjectException = exception as InvalidObjectElementException;
                if (invalidObjectException != null)
                {
                    var errors = new JArray();
                    foreach (var validationError in invalidObjectException.Errors)
                    {
                        errors.Add(validationError.SerializeToJson());
                    }

                    content.Add(
                        new JObject
                        {
                            [Tokens.IdToken] = invalidObjectException.ElementId,
                            [Tokens.ErrorsToken] = errors
                        });
                }
            }

            return content;
        }
    }
}

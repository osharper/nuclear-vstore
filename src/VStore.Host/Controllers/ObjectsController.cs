using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;
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
    [Route("api/{api-version:apiVersion}/objects")]
    public sealed class ObjectsController : VStoreController
    {
        private readonly ObjectsStorageReader _objectsStorageReader;
        private readonly ObjectsManagementService _objectsManagementService;
        private readonly ILogger<ObjectsController> _logger;

        public ObjectsController(
            ObjectsStorageReader objectsStorageReader,
            ObjectsManagementService objectsManagementService,
            ILogger<ObjectsController> logger)
        {
            _logger = logger;
            _objectsStorageReader = objectsStorageReader;
            _objectsManagementService = objectsManagementService;
        }

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyCollection<IdentifyableObjectDescriptor<long>>), 200)]
        public async Task<IActionResult> List([FromHeader(Name = Http.HeaderNames.AmsContinuationToken)]string continuationToken)
        {
            var container = await _objectsStorageReader.GetObjectMetadatas(continuationToken?.Trim('"'));

            if (!string.IsNullOrEmpty(container.ContinuationToken))
            {
                Response.Headers[Http.HeaderNames.AmsContinuationToken] = $"\"{container.ContinuationToken}\"";
            }

            return Json(container.Collection);
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
        }

        [HttpGet("{id}/versions")]
        [ProducesResponseType(typeof(IReadOnlyCollection<ModifiedObjectDescriptor>), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetVersions(long id)
        {
            try
            {
                var versions = await _objectsStorageReader.GetAllObjectRootVersions(id);
                return Json(versions);
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
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
        }

        [HttpPost("{id}")]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(typeof(object), 422)]
        public async Task<IActionResult> Create(
            long id,
            [FromHeader(Name = Http.HeaderNames.AmsAuthor)] string author,
            [FromBody] IObjectDescriptor objectDescriptor)
        {
            if (string.IsNullOrEmpty(author))
            {
                return BadRequest($"'{Http.HeaderNames.AmsAuthor}' request header must be specified.");
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
            catch (InvalidObjectElementException ex)
            {
                return Unprocessable(GenerateErrorJsonResult(ex));
            }
            catch (ObjectNotFoundException ex)
            {
                _logger.LogError(new EventId(), ex, "Error occured while creating object");
                return Unprocessable(ex.Message);
            }
            catch (ObjectAlreadyExistsException)
            {
                return Conflict("Object with the same id already exists");
            }
            catch (SessionLockAlreadyExistsException)
            {
                return Conflict("Simultaneous creation of object with the same id");
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(new EventId(), ex, "Error occured while creating object");
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
            [FromHeader(Name = Http.HeaderNames.AmsAuthor)] string author,
            [FromBody] IObjectDescriptor objectDescriptor)
        {
            if (string.IsNullOrEmpty(ifMatch))
            {
                return BadRequest($"'{HeaderNames.IfMatch}' request header must be specified.");
            }

            if (string.IsNullOrEmpty(author))
            {
                return BadRequest($"'{Http.HeaderNames.AmsAuthor}' request header must be specified.");
            }

            if (objectDescriptor == null)
            {
                return BadRequest("Object descriptor must be set.");
            }

            try
            {
                var latestVersionId = await _objectsManagementService.Modify(id, ifMatch.Trim('"'), author, objectDescriptor);
                var url = Url.AbsoluteAction("GetVersion", "Objects", new { id, versionId = latestVersionId });

                Response.Headers[HeaderNames.ETag] = $"\"{latestVersionId}\"";
                return NoContent(url);
            }
            catch (InvalidObjectElementException ex)
            {
                return Unprocessable(GenerateErrorJsonResult(ex));
            }
            catch (ObjectNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (SessionLockAlreadyExistsException)
            {
                return Conflict("Simultaneous modification of object");
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
        }

        private static JToken GenerateErrorJsonResult(InvalidObjectElementException ex)
        {
            var errors = new JArray();
            foreach (var validationError in ex.Errors)
            {
                errors.Add(validationError.SerializeToJson());
            }

            return new JArray
                       {
                           new JObject
                               {
                                   [Tokens.IdToken] = ex.ElementId,
                                   [Tokens.ErrorsToken] = errors
                               }
                       };
        }
    }
}

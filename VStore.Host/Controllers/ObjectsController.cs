using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Objects;
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
        public async Task<IActionResult> GetTemplateDescriptor(long id, string versionId)
        {
            try
            {
                var descriptor = await _objectsStorageReader.GetTemplateDescriptor(id, versionId);
                return Json(descriptor);
            }
            catch (ObjectNotFoundException)
            {
                return NotFound(id);
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(long id)
        {
            try
            {
                var descriptor = await _objectsStorageReader.GetObjectDescriptor(id, null);
                return Json(
                    new
                        {
                            descriptor.Id,
                            descriptor.VersionId,
                            descriptor.LastModified,
                            descriptor.TemplateId,
                            descriptor.TemplateVersionId,
                            descriptor.Language,
                            descriptor.Properties,
                            descriptor.Elements
                        });
            }
            catch (ObjectNotFoundException)
            {
                return NotFound(id);
            }
            catch (ObjectInconsistentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(0), ex, "Error occured while getting an object");
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("{id}/{versionId}")]
        public async Task<IActionResult> Get(long id, string versionId)
        {
            try
            {
                var descriptor = await _objectsStorageReader.GetObjectDescriptor(id, versionId);
                return Json(
                    new
                        {
                            descriptor.Id,
                            descriptor.VersionId,
                            descriptor.LastModified,
                            descriptor.TemplateId,
                            descriptor.TemplateVersionId,
                            descriptor.Language,
                            descriptor.Properties,
                            descriptor.Elements
                        });
            }
            catch (ObjectNotFoundException)
            {
                return NotFound(id);
            }
            catch (ObjectInconsistentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(0), ex, "Error occured while getting an object");
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> Create(long id, [FromBody] IObjectDescriptor objectDescriptor)
        {
            if (objectDescriptor == null)
            {
                return BadRequest("Incorrect object descriptor");
            }

            try
            {
                var versionId = await _objectsManagementService.Create(id, objectDescriptor);
                var url = Url.AbsoluteAction("Get", "Objects", new { id, versionId });
                return Created(url, versionId);
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
                _logger.LogError(new EventId(0), ex, "Unknown error occured while creating object");
                return InternalServerError();
            }
        }

        [HttpPut("{id}/{versionId}")]
        public async Task<IActionResult> Modify(long id, string versionId, [FromBody] IObjectDescriptor objectDescriptor)
        {
            if (objectDescriptor == null)
            {
                return BadRequest("Incorrect object descriptor");
            }

            try
            {
                var latestVersionId = await _objectsManagementService.ModifyElement(id, versionId, objectDescriptor);
                var url = Url.AbsoluteAction("Get", "Objects", new { id, versionId = latestVersionId });
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
                return Conflict();
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogError(new EventId(0), ex, "Error occured while modifying object");
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
                _logger.LogError(new EventId(0), ex, "Unknown error occured while modifying object");
                return InternalServerError();
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

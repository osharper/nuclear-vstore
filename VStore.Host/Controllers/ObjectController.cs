using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.Json;
using NuClear.VStore.Objects;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.S3;

namespace NuClear.VStore.Host.Controllers
{
    [Route("object")]
    public sealed class ObjectController : Controller
    {
        private readonly ObjectStorageReader _objectStorageReader;
        private readonly ObjectManagementService _objectManagementService;
        private readonly ILogger<ObjectController> _logger;

        public ObjectController(ObjectStorageReader objectStorageReader, ObjectManagementService objectManagementService, ILogger<ObjectController> logger)
        {
            _logger = logger;
            _objectStorageReader = objectStorageReader;
            _objectManagementService = objectManagementService;
        }

        [HttpGet("template/{id}/{versionId}")]
        public async Task<IActionResult> GetTemplateDescriptor(long id, string versionId)
        {
            try
            {
                var descriptor = await _objectStorageReader.GetTemplateDescriptor(id, versionId);
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
                var descriptor = await _objectStorageReader.GetObjectDescriptor(id, null);
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
                var descriptor = await _objectStorageReader.GetObjectDescriptor(id, versionId);
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
                var versionId = await _objectManagementService.Create(id, objectDescriptor);
                var url = Url.AbsoluteAction("Get", "Object", new { id, versionId });
                return Created(url, versionId);
            }
            catch (AggregateException ex)
            {
                return new JsonResult(GenerateErrorJsonResult(ex))
                {
                    StatusCode = 422
                };
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
                return new StatusCodeResult(500);
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

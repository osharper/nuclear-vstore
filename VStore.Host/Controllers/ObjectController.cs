using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.Objects;
using NuClear.VStore.Objects.Validate;
using NuClear.VStore.Objects.Validate.Exceptions;
using NuClear.VStore.S3;

namespace NuClear.VStore.Host.Controllers
{
    [Route("object")]
    public sealed class ObjectController : Controller
    {
        private readonly ObjectStorageReader _objectStorageReader;
        private readonly ObjectManagementService _objectManagementService;

        public ObjectController(ObjectStorageReader objectStorageReader, ObjectManagementService objectManagementService)
        {
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
                        descriptor.Properties,
                        descriptor.Elements
                    });
            }
            catch (ObjectNotFoundException)
            {
                return NotFound(id);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{id}")]
        public async Task<IActionResult> Create(long id, [FromBody] IObjectDescriptor objectDescriptor)
        {
            try
            {
                var versionId = await _objectManagementService.Create(id, objectDescriptor);
                var url = Url.AbsoluteAction("Get", "Object", new { id, versionId });
                return Created(url, versionId);
            }
            catch (AggregateException exc)
            {
                return new JsonResult(GenerateErrorJsonResult(exc))
                {
                    StatusCode = 422
                };
            }
            catch (InvalidOperationException exc)
            {
                return BadRequest(exc.Message);
            }
            catch (Exception)
            {
                return new StatusCodeResult(500);
            }
        }

        private static JToken GenerateErrorJsonResult(AggregateException exc)
        {
            var content = new JArray();
            foreach (var exception in exc.InnerExceptions)
            {
                var invalidObjectException = exception as InvalidObjectElementException;
                if (invalidObjectException != null)
                {
                    var errors = new JArray();

                    foreach (var validateException in invalidObjectException.InnerExceptions)
                    {
                        errors.Add(GenerateErrorJson(validateException));
                    }

                    content.Add(
                        new JObject
                        {
                            ["id"] = invalidObjectException.ElementId,
                            ["errors"] = errors
                        });
                }
            }

            return content;
        }

        private static JToken GenerateErrorJson(Exception exception)
        {
            var validateException = exception as ObjectElementValidationException;
            if (validateException == null)
            {
                return new JObject
                {
                    ["type"] = exception.GetType().Name,
                    ["value"] = exception.Message
                };
            }

            switch (validateException.ErrorType)
            {
                case ElementValidationErrors.ControlСharactersInText:
                    return new JObject
                    {
                        ["type"] = "controlСharacters",
                        ["value"] = true
                    };

                case ElementValidationErrors.NonBreakingSpaceSymbol:
                    return new JObject
                    {
                        ["type"] = "nonBreakingSpaceSymbol",
                        ["value"] = true
                    };

                case ElementValidationErrors.WordsTooLong:
                    return new JObject
                    {
                        ["type"] = "maxSymbolsPerWord",
                        ["value"] = new JArray((validateException as ElementWordsTooLongException)?.TooLongWords)
                    };

                case ElementValidationErrors.TextTooLong:
                    return new JObject
                    {
                        ["type"] = "maxSymbols",
                        ["value"] = (validateException as ElementTextTooLongException)?.ActualLength
                    };

                case ElementValidationErrors.NestedList:
                    return new JObject
                    {
                        ["type"] = "nestedList",
                        ["value"] = true
                    };

                case ElementValidationErrors.UnsupportedTags:
                    return new JObject
                    {
                        ["type"] = "unsupportedTags",
                        ["value"] = new JArray((validateException as UnsupportedTagsException)?.UnsupportedTags)
                    };

                case ElementValidationErrors.UnsupportedAttributes:
                    return new JObject
                    {
                        ["type"] = "unsupportedAttributes",
                        ["value"] = new JArray((validateException as UnsupportedAttributesException)?.UnsupportedAttributes)
                    };

                case ElementValidationErrors.UnsupportedListElements:
                    return new JObject
                    {
                        ["type"] = "unsupportedListElements",
                        ["value"] = true
                    };

                case ElementValidationErrors.InvalidHtml:
                    return new JObject
                    {
                        ["type"] = "invalidHtml",
                        ["value"] = true
                    };

                case ElementValidationErrors.EmptyList:
                    return new JObject
                    {
                        ["type"] = "emptyList",
                        ["value"] = true
                    };

                case ElementValidationErrors.TooManyLines:
                    return new JObject
                    {
                        ["type"] = "maxLines",
                        ["value"] = (validateException as TooManyLinesException)?.ActualLinesCount
                    };

                case ElementValidationErrors.IncorrectLink:
                    return new JObject
                    {
                        ["type"] = "incorrectLink",
                        ["value"] = true
                    };

                default:
                    throw new ArgumentOutOfRangeException(nameof(exception), validateException.ErrorType, "Unsupported validation error type");
            }
        }
    }
}
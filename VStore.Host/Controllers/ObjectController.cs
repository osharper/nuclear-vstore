using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.Json;
using NuClear.VStore.Objects;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.Exceptions;
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
                            [Tokens.IdToken] = invalidObjectException.ElementId,
                            [Tokens.ErrorsToken] = errors
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
                    [Tokens.TypeToken] = exception.GetType().Name,
                    [Tokens.ValueToken] = exception.Message
                };
            }

            switch (validateException.ErrorType)
            {
                case ElementValidationErrors.WordsTooLong:
                    return new JObject
                    {
                        [Tokens.TypeToken] = "maxSymbolsPerWord",
                        [Tokens.ValueToken] = new JArray((validateException as ElementWordsTooLongException)?.TooLongWords)
                    };

                case ElementValidationErrors.TextTooLong:
                    return new JObject
                    {
                        [Tokens.TypeToken] = "maxSymbols",
                        [Tokens.ValueToken] = (validateException as ElementTextTooLongException)?.ActualLength
                    };

                case ElementValidationErrors.UnsupportedTags:
                    return new JObject
                    {
                        [Tokens.TypeToken] = "unsupportedTags",
                        [Tokens.ValueToken] = new JArray((validateException as UnsupportedTagsException)?.UnsupportedTags)
                    };

                case ElementValidationErrors.UnsupportedAttributes:
                    return new JObject
                    {
                        [Tokens.TypeToken] = "unsupportedAttributes",
                        [Tokens.ValueToken] = new JArray((validateException as UnsupportedAttributesException)?.UnsupportedAttributes)
                    };

                case ElementValidationErrors.TooManyLines:
                    return new JObject
                    {
                        [Tokens.TypeToken] = "maxLines",
                        [Tokens.ValueToken] = (validateException as TooManyLinesException)?.ActualLinesCount
                    };

                case ElementValidationErrors.IncorrectPeriod:
                    return new JObject
                    {
                        [Tokens.TypeToken] = "incorrectPeriod",
                        [Tokens.ValueToken] = (validateException as IncorrectPeriodException)?.DatesDifference.TotalDays
                    };

                case ElementValidationErrors.UnsupportedListElements:
                case ElementValidationErrors.InvalidHtml:
                case ElementValidationErrors.EmptyList:
                case ElementValidationErrors.IncorrectLink:
                case ElementValidationErrors.NestedList:
                case ElementValidationErrors.ControlСharacters:
                case ElementValidationErrors.NonBreakingSpaceSymbol:
                case ElementValidationErrors.InvalidDateRange:
                    {
                        var error = validateException.ErrorType.ToString();
                        return new JObject
                        {
                            [Tokens.TypeToken] = char.ToLower(error[0]) + error.Substring(1),
                            [Tokens.ValueToken] = true
                        };
                    }

                default:
                    throw new ArgumentOutOfRangeException(nameof(exception), validateException.ErrorType, "Unsupported validation error type");
            }
        }
    }
}
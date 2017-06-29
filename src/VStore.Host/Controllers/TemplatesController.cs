using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.Json;
using NuClear.VStore.Locks;
using NuClear.VStore.Objects;
using NuClear.VStore.S3;
using NuClear.VStore.Templates;

namespace NuClear.VStore.Host.Controllers
{
    [ApiVersion("1.1")]
    [ApiVersion("1.0", Deprecated = true)]
    [Route("api/{api-version:apiVersion}/templates")]
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
        public IActionResult GetAvailableElementDescriptors()
        {
            return Json(_templatesManagementService.GetAvailableElementDescriptors());
        }

        [HttpGet]
        [ProducesResponseType(typeof(IReadOnlyCollection<IdentifyableObjectDescriptor<long>>), 200)]
        public async Task<IActionResult> List([FromHeader(Name = Http.HeaderNames.AmsContinuationToken)]string continuationToken)
        {
            var container = await _templatesStorageReader.GetTemplateMetadatas(continuationToken?.Trim('"'));

            if (!string.IsNullOrEmpty(container.ContinuationToken))
            {
                Response.Headers[Http.HeaderNames.AmsContinuationToken] = $"\"{container.ContinuationToken}\"";
            }

            return Json(container.Collection);
        }

        [HttpGet("specified")]
        [ProducesResponseType(typeof(IReadOnlyCollection<ModifiedTemplateDescriptor>), 200)]
        public async Task<IActionResult> List(IReadOnlyCollection<long> ids)
        {
            var descriptors = await _templatesStorageReader.GetTemplateMetadatas(ids);
            return Json(descriptors);
        }

        [HttpGet("{id:long}")]
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
                            templateDescriptor.AuthorLogin,
                            templateDescriptor.AuthorName,
                            templateDescriptor.Properties,
                            templateDescriptor.Elements
                        });
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
            }
        }

        [HttpGet("{id:long}/{versionId}")]
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
                            templateDescriptor.AuthorLogin,
                            templateDescriptor.AuthorName,
                            templateDescriptor.Properties,
                            templateDescriptor.Elements
                        });
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
            }
        }

        [Obsolete, MapToApiVersion("1.0")]
        [HttpPost("validate-elements")]
        [ProducesResponseType(200)]
        [ProducesResponseType(typeof(object), 422)]
        public async Task<IActionResult> ValidateElementsV10([FromBody] IReadOnlyCollection<IElementDescriptor> elementDescriptors)
        {
            try
            {
                await _templatesManagementService.VerifyElementDescriptorsConsistency(elementDescriptors);
                return Ok();
            }
            catch (TemplateValidationException ex)
            {
                return Unprocessable(GenerateTemplateErrorJsonV10(ex));
            }
        }

        [MapToApiVersion("1.1")]
        [HttpPost("validate-elements")]
        [ProducesResponseType(200)]
        [ProducesResponseType(typeof(object), 422)]
        public async Task<IActionResult> ValidateElements([FromBody] IReadOnlyCollection<IElementDescriptor> elementDescriptors)
        {
            try
            {
                await _templatesManagementService.VerifyElementDescriptorsConsistency(elementDescriptors);
                return Ok();
            }
            catch (TemplateValidationException ex)
            {
                return Unprocessable(GenerateTemplateErrorJson(ex));
            }
        }

        [Obsolete, MapToApiVersion("1.0")]
        [HttpPost("{id:long}")]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(409)]
        [ProducesResponseType(typeof(object), 422)]
        public async Task<IActionResult> CreateV10(
            long id,
            [FromHeader(Name = Http.HeaderNames.AmsAuthor)] string author,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorLogin)] string authorLogin,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorName)] string authorName,
            [FromBody] ITemplateDescriptor templateDescriptor)
        {
            return await CreateInternal(id, author, authorLogin, authorName, templateDescriptor, GenerateTemplateErrorJsonV10);
        }

        [MapToApiVersion("1.1")]
        [HttpPost("{id:long}")]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(object), 400)]
        [ProducesResponseType(409)]
        [ProducesResponseType(typeof(object), 422)]
        public async Task<IActionResult> Create(
            long id,
            [FromHeader(Name = Http.HeaderNames.AmsAuthor)] string author,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorLogin)] string authorLogin,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorName)] string authorName,
            [FromBody] ITemplateDescriptor templateDescriptor)
        {
            return await CreateInternal(id, author, authorLogin, authorName, templateDescriptor, GenerateTemplateErrorJson);
        }

        [Obsolete, MapToApiVersion("1.0")]
        [HttpPut("{id:long}")]
        [ProducesResponseType(204)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(404)]
        [ProducesResponseType(409)]
        [ProducesResponseType(412)]
        [ProducesResponseType(typeof(object), 422)]
        public async Task<IActionResult> ModifyV10(
            long id,
            [FromHeader(Name = HeaderNames.IfMatch)] string ifMatch,
            [FromHeader(Name = Http.HeaderNames.AmsAuthor)] string author,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorLogin)] string authorLogin,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorName)] string authorName,
            [FromBody] ITemplateDescriptor templateDescriptor)
        {
            return await ModifyInternal(id, ifMatch, author, authorLogin, authorName, templateDescriptor, GenerateTemplateErrorJsonV10);
        }

        [MapToApiVersion("1.1")]
        [HttpPut("{id:long}")]
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
            [FromHeader(Name = Http.HeaderNames.AmsAuthorLogin)] string authorLogin,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorName)] string authorName,
            [FromBody] ITemplateDescriptor templateDescriptor)
        {
            return await ModifyInternal(id, ifMatch, author, authorLogin, authorName, templateDescriptor, GenerateTemplateErrorJson);
        }

        private async Task<IActionResult> CreateInternal(
            long id,
            string author,
            string authorLogin,
            string authorName,
            ITemplateDescriptor templateDescriptor,
            Func<TemplateValidationException, JToken> errorGenerator)
        {
            if (string.IsNullOrEmpty(author) || string.IsNullOrEmpty(authorLogin) || string.IsNullOrEmpty(authorName))
            {
                return BadRequest(
                    $"'{Http.HeaderNames.AmsAuthor}', '{Http.HeaderNames.AmsAuthorLogin}' and '{Http.HeaderNames.AmsAuthorName}' " +
                    "request headers must be specified.");
            }

            if (templateDescriptor == null)
            {
                return BadRequest("Template descriptor must be set.");
            }

            try
            {
                var versionId = await _templatesManagementService.CreateTemplate(id, new AuthorInfo(author, authorLogin, authorName), templateDescriptor);
                var url = Url.AbsoluteAction("GetVersion", "Templates", new { id, versionId });

                Response.Headers[HeaderNames.ETag] = $"\"{versionId}\"";
                return Created(url, null);
            }
            catch (ObjectAlreadyExistsException)
            {
                return Conflict("Template with the same id already exists");
            }
            catch (SessionLockAlreadyExistsException)
            {
                return Conflict("Simultaneous creation of template with the same id");
            }
            catch (TemplateValidationException ex)
            {
                return Unprocessable(errorGenerator(ex));
            }
        }

        private async Task<IActionResult> ModifyInternal(
            long id,
            string ifMatch,
            string author,
            string authorLogin,
            string authorName,
            ITemplateDescriptor templateDescriptor,
            Func<TemplateValidationException, JToken> errorGenerator)
        {
            if (string.IsNullOrEmpty(ifMatch))
            {
                return BadRequest($"'{HeaderNames.IfMatch}' request header must be specified.");
            }

            if (string.IsNullOrEmpty(author) || string.IsNullOrEmpty(authorLogin) || string.IsNullOrEmpty(authorName))
            {
                return BadRequest(
                    $"'{Http.HeaderNames.AmsAuthor}', '{Http.HeaderNames.AmsAuthorLogin}' and '{Http.HeaderNames.AmsAuthorName}' " +
                    "request headers must be specified.");
            }

            if (templateDescriptor == null)
            {
                return BadRequest("Template descriptor must be set.");
            }

            try
            {
                var latestVersionId = await _templatesManagementService.ModifyTemplate(
                                          id,
                                          ifMatch.Trim('"'),
                                          new AuthorInfo(author, authorLogin, authorName),
                                          templateDescriptor);
                var url = Url.AbsoluteAction("GetVersion", "Templates", new { id, versionId = latestVersionId });

                Response.Headers[HeaderNames.ETag] = $"\"{latestVersionId}\"";
                return NoContent(url);
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
            }
            catch (TemplateValidationException ex)
            {
                return Unprocessable(errorGenerator(ex));
            }
            catch (SessionLockAlreadyExistsException)
            {
                return Conflict("Simultaneous modification of template");
            }
            catch (ConcurrencyException)
            {
                return PreconditionFailed();
            }
        }

        private static JToken GenerateTemplateErrorJsonV10(TemplateValidationException ex) => new JArray { ex.SerializeToJsonV10() };

        private static JToken GenerateTemplateErrorJson(TemplateValidationException ex) =>
            new JObject
                {
                    { Tokens.ErrorsToken, new JArray() },
                    { Tokens.ElementsToken, new JArray { ex.SerializeToJson() } }
                };
    }
}

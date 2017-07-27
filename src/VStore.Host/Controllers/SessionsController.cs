using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

using Newtonsoft.Json.Linq;

using NuClear.VStore.DataContract;
using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.Host.Filters;
using NuClear.VStore.Json;
using NuClear.VStore.S3;
using NuClear.VStore.Sessions;

namespace NuClear.VStore.Host.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/{api-version:apiVersion}/sessions")]
    public sealed class SessionsController : VStoreController
    {
        private readonly SessionManagementService _sessionManagementService;
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(SessionManagementService sessionManagementService, ILogger<SessionsController> logger)
        {
            _sessionManagementService = sessionManagementService;
            _logger = logger;
        }

        [HttpGet("{sessionId:guid}")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(410)]
        public async Task<IActionResult> Get(Guid sessionId)
        {
            try
            {
                var sessionContext = await _sessionManagementService.GetSessionContext(sessionId);

                var templateDescriptor = sessionContext.TemplateDescriptor;
                var uploadUrls = UploadUrl.Generate(
                    templateDescriptor,
                    templateCode => Url.Action(
                        "UploadFile",
                        new
                        {
                            sessionId,
                            templateCode
                        }));

                Response.Headers[HeaderNames.ETag] = $"\"{sessionId}\"";
                Response.Headers[HeaderNames.Expires] = sessionContext.ExpiresAt.ToString("R");

                return Json(
                    new
                        {
                            sessionContext.AuthorInfo.Author,
                            sessionContext.AuthorInfo.AuthorLogin,
                            sessionContext.AuthorInfo.AuthorName,
                            sessionContext.Language,
                            Template = new
                                {
                                    Id = sessionContext.TemplateId,
                                    templateDescriptor.VersionId,
                                    templateDescriptor.LastModified,
                                    templateDescriptor.Author,
                                    templateDescriptor.AuthorLogin,
                                    templateDescriptor.AuthorName,
                                    templateDescriptor.Properties,
                                    templateDescriptor.Elements
                                },
                            uploadUrls
                        });
            }
            catch (ObjectNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (SessionExpiredException ex)
            {
                return Gone(ex.ExpiredAt);
            }
        }

        [HttpPost("{language:lang}/{templateId:long}")]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 404)]
        public async Task<IActionResult> SetupSession(
            [FromHeader(Name = Http.HeaderNames.AmsAuthor)] string author,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorLogin)] string authorLogin,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorName)] string authorName,
            Language language,
            long templateId)
        {
            if (string.IsNullOrEmpty(author) || string.IsNullOrEmpty(authorLogin) || string.IsNullOrEmpty(authorName))
            {
                return BadRequest(
                    $"'{Http.HeaderNames.AmsAuthor}', '{Http.HeaderNames.AmsAuthorLogin}' and '{Http.HeaderNames.AmsAuthorName}' " +
                    "request headers must be specified.");
            }

            try
            {
                var sessionId = Guid.NewGuid();
                await _sessionManagementService.Setup(sessionId, templateId, null, language, new AuthorInfo(author, authorLogin, authorName));
                var url = Url.AbsoluteAction("Get", "Sessions", new { sessionId });

                Response.Headers[HeaderNames.ETag] = $"\"{sessionId}\"";
                return Created(url,  null);
            }
            catch (ObjectNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (SessionCannotBeCreatedException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("{language:lang}/{templateId:long}/{templateVersionId}")]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 404)]
        public async Task<IActionResult> SetupSession(
            [FromHeader(Name = Http.HeaderNames.AmsAuthor)] string author,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorLogin)] string authorLogin,
            [FromHeader(Name = Http.HeaderNames.AmsAuthorName)] string authorName,
            Language language,
            long templateId,
            string templateVersionId)
        {
            if (string.IsNullOrEmpty(author) || string.IsNullOrEmpty(authorLogin) || string.IsNullOrEmpty(authorName))
            {
                return BadRequest(
                    $"'{Http.HeaderNames.AmsAuthor}', '{Http.HeaderNames.AmsAuthorLogin}' and '{Http.HeaderNames.AmsAuthorName}' " +
                    "request headers must be specified.");
            }

            try
            {
                var sessionId = Guid.NewGuid();
                await _sessionManagementService.Setup(sessionId, templateId, templateVersionId, language, new AuthorInfo(author, authorLogin, authorName));
                var url = Url.AbsoluteAction("Get", "Sessions", new { sessionId });

                Response.Headers[HeaderNames.ETag] = $"\"{sessionId}\"";
                return Created(url,  null);
            }
            catch (ObjectNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (SessionCannotBeCreatedException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpPost("{sessionId:guid}/upload/{templateCode:int}")]
        [DisableFormValueModelBinding]
        [MultipartBodyLengthLimit]
        [ProducesResponseType(typeof(UploadedFileValue), 201)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(string), 410)]
        [ProducesResponseType(typeof(object), 422)]
        [ProducesResponseType(typeof(string), 452)]
        public async Task<IActionResult> UploadFile(Guid sessionId, int templateCode)
        {
            var multipartBoundary = Request.GetMultipartBoundary();
            if (string.IsNullOrEmpty(multipartBoundary))
            {
                return BadRequest($"Expected a multipart request, but got '{Request.ContentType}'.");
            }

            MultipartUploadSession uploadSession = null;
            try
            {
                var formFeature = Request.HttpContext.Features.Get<IFormFeature>();
                var form = await formFeature.ReadFormAsync(CancellationToken.None);

                if (form.Files.Count != 1)
                {
                    return BadRequest("Request body must contain single file section.");
                }

                var file = form.Files.First();
                uploadSession = await _sessionManagementService.InitiateMultipartUpload(
                                    sessionId,
                                    file.FileName,
                                    file.ContentType,
                                    file.Length,
                                    templateCode);
                _logger.LogInformation("Multipart upload for file '{fileName}' in session '{sessionId}' was initiated.", file.FileName, sessionId);

                using (var inputStream = file.OpenReadStream())
                {
                    await _sessionManagementService.UploadFilePart(uploadSession, inputStream, templateCode);
                }

                var uploadedFileInfo = await _sessionManagementService.CompleteMultipartUpload(uploadSession, templateCode);

                return Created(uploadedFileInfo.DownloadUri, new UploadedFileValue(uploadedFileInfo.Id));
            }
            catch (ObjectNotFoundException)
            {
                return NotFound();
            }
            catch (SessionExpiredException ex)
            {
                return Gone(ex.ExpiredAt);
            }
            catch (InvalidTemplateException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidDataException ex)
            {
                return RequestTooLarge(ex.Message);
            }
            catch (MissingFilenameException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidBinaryException ex)
            {
                return Unprocessable(GenerateErrorJsonResult(ex));
            }
            finally
            {
                if (uploadSession != null)
                {
                    await _sessionManagementService.AbortMultipartUpload(uploadSession);
                }
            }
        }

        private static JToken GenerateErrorJsonResult(InvalidBinaryException ex) =>
            new JObject
                {
                    { Tokens.ErrorsToken, new JArray() },
                    { Tokens.ElementsToken, new JArray { ex.SerializeToJson() } }
                };

        private sealed class UploadedFileValue : IObjectElementRawValue
        {
            public UploadedFileValue(string raw)
            {
                Raw = raw;
            }

            public string Raw { get; }
        }
    }
}

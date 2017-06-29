using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.Host.Filters;
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
                Response.Headers[Http.HeaderNames.AmsAuthor] = sessionContext.AuthorInfo.Author;
                Response.Headers[Http.HeaderNames.AmsAuthorLogin] = sessionContext.AuthorInfo.AuthorLogin;
                Response.Headers[Http.HeaderNames.AmsAuthorName] = sessionContext.AuthorInfo.AuthorName;
                return Json(
                    new
                        {
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
        [ProducesResponseType(typeof(string), 422)]
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
                return Unprocessable(ex.Message);
            }
        }

        [HttpPost("{language:lang}/{templateId:long}/{templateVersionId}")]
        [ProducesResponseType(201)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(string), 422)]
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
                return Unprocessable(ex.Message);
            }
        }

        [AllowAnonymous]
        [HttpPost("{sessionId:guid}/upload/{templateCode:int}")]
        [DisableFormValueModelBinding]
        [MultipartBodyLengthLimit(1024)]
        [ProducesResponseType(typeof(UploadedFileValue), 201)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(object), 422)]
        public async Task<IActionResult> UploadFile(Guid sessionId, int templateCode)
        {
            var multipartBoundary = Request.GetMultipartBoundary();
            if (string.IsNullOrEmpty(multipartBoundary))
            {
                return BadRequest($"Expected a multipart request, but got '{Request.ContentType}'.");
            }

            MultipartUploadSession uploadSession = null;
            var reader = new MultipartReader(multipartBoundary, HttpContext.Request.Body);
            var section = await reader.ReadNextSectionAsync();
            var contentLength = HttpContext.Request.ContentLength;
            if (section == null || contentLength == null)
            {
                return BadRequest("Request body is empty or doesn't contain sections.");
            }

            try
            {
                for (; section != null; section = await reader.ReadNextSectionAsync())
                {
                    var fileSection = section.AsFileSection();
                    if (fileSection == null)
                    {
                        if (uploadSession != null)
                        {
                            await _sessionManagementService.AbortMultipartUpload(uploadSession);
                        }

                        return BadRequest("File upload supported only during single request.");
                    }

                    if (uploadSession == null)
                    {
                        uploadSession = await _sessionManagementService.InitiateMultipartUpload(
                                            sessionId,
                                            fileSection.FileName,
                                            section.ContentType,
                                            templateCode);
                        _logger.LogInformation($"Multipart upload for file '{fileSection.FileName}' was initiated.");
                    }

                    using (fileSection.FileStream)
                    {
                        await _sessionManagementService.UploadFilePart(uploadSession, fileSection.FileStream, templateCode);
                    }
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
                return Unprocessable(ex.Message);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return Unprocessable(ex.Message);
            }
            catch (FilesizeMismatchException ex)
            {
                return Unprocessable(ex.Message);
            }
            catch (ImageIncorrectException ex)
            {
                return Unprocessable(ex.Message);
            }
            catch (ArticleIncorrectException ex)
            {
                return Unprocessable(ex.Message);
            }
            finally
            {
                if (uploadSession != null)
                {
                    await _sessionManagementService.AbortMultipartUpload(uploadSession);
                }
            }
        }

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

using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Sessions;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.Host.Filters;
using NuClear.VStore.S3;
using NuClear.VStore.Sessions;

namespace NuClear.VStore.Host.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/{version:apiVersion}/sessions")]
    public sealed class SessionsController : VStoreController
    {
        private readonly SessionManagementService _sessionManagementService;
        private readonly ILogger<SessionsController> _logger;

        public SessionsController(SessionManagementService sessionManagementService, ILogger<SessionsController> logger)
        {
            _sessionManagementService = sessionManagementService;
            _logger = logger;
        }

        [HttpGet("{sessionId}")]
        [ProducesResponseType(typeof(SessionDescriptor), 200)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(410)]
        public async Task<IActionResult> Get(Guid sessionId)
        {
            try
            {
                var sessionContext = await _sessionManagementService.GetSessionContext(sessionId);

                Response.Headers[HeaderNames.Expires] = sessionContext.ExpiresAt.ToString("R");
                Response.Headers[Headers.HeaderNames.AmsAuthor] = sessionContext.Author;

                return Json(sessionContext.Descriptor);
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

        [HttpPost("{templateId}/{language}")]
        [ProducesResponseType(typeof(object), 201)]
        [ProducesResponseType(typeof(string), 404)]
        [ProducesResponseType(typeof(string), 422)]
        public async Task<IActionResult> SetupSession(
            [FromHeader(Name = Headers.HeaderNames.AmsAuthor)] string author,
            long templateId,
            Language language)
        {
            try
            {
                var sessionSetupContext = await _sessionManagementService.Setup(templateId, language, author);
                var url = Url.AbsoluteAction("Get", "Sessions", new { sessionId = sessionSetupContext.Id });

                var templateDescriptor = sessionSetupContext.TemplateDescriptor;
                var uploadUrls = UploadUrl.Generate(
                    templateDescriptor,
                    templateCode => Url.Action(
                        "UploadFile",
                        new
                            {
                                sessionId = sessionSetupContext.Id,
                                templateCode
                            }));

                Response.Headers[HeaderNames.Expires] = sessionSetupContext.ExpiresAt.ToString("R");
                return Created(
                    url,
                    new
                        {
                            Template = new
                                           {
                                               Id = templateId,
                                               templateDescriptor.VersionId,
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
            catch (SessionCannotBeCreatedException ex)
            {
                return Unprocessable(ex.Message);
            }
        }

        [HttpPost("{sessionId}/upload/{templateCode}")]
        [DisableFormValueModelBinding]
        [MultipartBodyLengthLimit(1024)]
        [ProducesResponseType(typeof(UploadedFileInfo), 201)]
        [ProducesResponseType(typeof(string), 400)]
        [ProducesResponseType(typeof(string), 422)]
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
                                            contentLength.Value,
                                            templateCode);
                        _logger.LogInformation($"Multipart upload for file '{fileSection.FileName}' was initiated.");
                    }

                    using (fileSection.FileStream)
                    {
                        await _sessionManagementService.UploadFilePart(uploadSession, fileSection.FileStream, templateCode);
                    }
                }

                var uploadedFileInfo = await _sessionManagementService.CompleteMultipartUpload(uploadSession, templateCode);
                return Created(
                    uploadedFileInfo.PreviewUri,
                    new
                        {
                            uploadedFileInfo.Id,
                            uploadedFileInfo.FileName,
                            uploadedFileInfo.PreviewUri
                        });
            }
            catch (Exception ex)
            {
                _logger.LogError(new EventId(0), ex, "Error occured while file uploading.");
                if (uploadSession != null)
                {
                    await _sessionManagementService.AbortMultipartUpload(uploadSession);
                }

                return Unprocessable(ex.Message);
            }
        }
    }
}

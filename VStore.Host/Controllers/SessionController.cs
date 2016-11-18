using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

using NuClear.VStore.Host.Filters;
using NuClear.VStore.Sessions;

namespace NuClear.VStore.Host.Controllers
{
    [Route("session")]
    public sealed class SessionController : Controller
    {
        private readonly SessionManagementService _sessionManagementService;
        private readonly ILogger<SessionController> _logger;

        public SessionController(SessionManagementService sessionManagementService, ILogger<SessionController> logger)
        {
            _sessionManagementService = sessionManagementService;
            _logger = logger;
        }

        [HttpPost("{templateId}")]
        public async Task<IActionResult> SetupSession(long templateId)
        {
            try
            {
                var sessionDescriptor = await _sessionManagementService.Setup(templateId);
                return Json(
                    new
                        {
                            Template = new
                                           {
                                               sessionDescriptor.TemplateDescriptor.Id,
                                               sessionDescriptor.TemplateDescriptor.VersionId,
                                               sessionDescriptor.TemplateDescriptor.LastModified,
                                               sessionDescriptor.TemplateDescriptor.Properties,
                                               sessionDescriptor.TemplateDescriptor.Elements
                                           },
                            sessionDescriptor.UploadUris,
                            sessionDescriptor.ExpiresAt
                        });
            }
            catch (SessionCannotBeCreatedException ex)
            {
                return StatusCode(422, ex.Message);
            }
        }

        [HttpPost("{sessionId}/{templateId}/{templateVersionId}/{templateCode}")]
        [DisableFormValueModelBinding]
        [MultipartBodyLengthLimit(1024)]
        public async Task<IActionResult> UploadFile(Guid sessionId, long templateId, string templateVersionId, int templateCode)
        {
            var multipartBoundary = Request.GetMultipartBoundary();
            if (string.IsNullOrEmpty(multipartBoundary))
            {
                return BadRequest($"Expected a multipart request, but got '{Request.ContentType}'.");
            }

            MultipartUploadSession uploadSession = null;
            var reader = new MultipartReader(multipartBoundary, HttpContext.Request.Body);
            var section = await reader.ReadNextSectionAsync();
            if (section == null)
            {
                return BadRequest("Request body doesn't contain sections.");
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
                        uploadSession = await _sessionManagementService.InitiateMultipartUpload(sessionId, fileSection.FileName, section.ContentType);
                        _logger.LogInformation($"Multipart upload for file '{fileSection.FileName}' was initiated.");
                    }

                    await _sessionManagementService.UploadFilePart(uploadSession, fileSection.FileStream);
                }

                var uploadedFileInfo = await _sessionManagementService.CompleteMultipartUpload(uploadSession, templateId, templateVersionId, templateCode);
                return Json(
                    new
                        {
                            uploadedFileInfo.Id,
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

                return StatusCode(422, ex.Message);
            }
        }
    }
}
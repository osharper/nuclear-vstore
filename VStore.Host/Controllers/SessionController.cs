using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Amazon.S3.Model;

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
        [MultipartBodyLengthLimit(int.MaxValue)]
        public async Task<IActionResult> UploadFile(Guid sessionId, long templateId, string templateVersionId, int templateCode)
        {
            var multipartBoundary = Request.GetMultipartBoundary();
            if (string.IsNullOrEmpty(multipartBoundary))
            {
                return BadRequest($"Expected a multipart request, but got '{Request.ContentType}'.");
            }

            var filesToUpload = new Dictionary<string, Tuple<string, List<PartETag>>>();

            var reader = new MultipartReader(multipartBoundary, HttpContext.Request.Body);
            MultipartSection section;
            var partNumber = 0;
            do
            {
                section = await reader.ReadNextSectionAsync();
                var fileSection = section?.AsFileSection();

                if (fileSection != null)
                {
                    string uploadId;

                    var fileName = fileSection.FileName;
                    if (!filesToUpload.ContainsKey(fileName))
                    {
                        uploadId = await _sessionManagementService.InitiateMultipartUpload(
                                       sessionId,
                                       templateId,
                                       templateVersionId,
                                       templateCode,
                                       fileName,
                                       section.ContentType);
                        filesToUpload.Add(fileName, Tuple.Create(uploadId, new List<PartETag>()));

                        _logger.LogInformation($"Multipart upload for file '{fileName}' initiated.");
                    }
                    else
                    {
                        uploadId = filesToUpload[fileName].Item1;
                    }

                    var etag = await _sessionManagementService.UploadFilePart(
                                  sessionId,
                                  fileName,
                                  uploadId,
                                  ++partNumber,
                                  section.BaseStreamOffset,
                                  fileSection.FileStream);
                    filesToUpload[fileName].Item2.Add(new PartETag(partNumber, etag));
                }
            }
            while (section != null);

            var etags = new List<string>();
            foreach (var fileInfo in filesToUpload)
            {
                var etag = await _sessionManagementService.CompleteMultipartUpload(
                    sessionId,
                    templateId,
                    templateVersionId,
                    templateCode,
                    fileInfo.Key,
                    fileInfo.Value.Item1,
                    fileInfo.Value.Item2);
                etags.Add(etag);
            }

            return Ok(etags);
        }
    }
}
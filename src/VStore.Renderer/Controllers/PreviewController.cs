using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Http.Core.Controllers;
using NuClear.VStore.ImageRendering;

namespace NuClear.VStore.Renderer.Controllers
{
    [ApiVersion("2.0")]
    [ApiVersion("1.0", Deprecated = true)]
    [Route("api/{api-version:apiVersion}/previews")]
    [Route("previews")]
    public class PreviewController : VStoreController
    {
        private readonly ImagePreviewService _imagePreviewService;

        public PreviewController(ImagePreviewService imagePreviewService)
        {
            _imagePreviewService = imagePreviewService;
        }

        [MapToApiVersion("2.0")]
        [HttpGet("{id:long}/{versionId}/{templateCode:int}/{width:int}x{height:int}")]
        [ProducesResponseType(typeof(byte[]), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> Get(long id, string versionId, int templateCode, int width, int height)
        {
            var (imageStream, contentType) = await _imagePreviewService.GetPreview(id, versionId, templateCode, width, height);
            return new FileStreamResult(imageStream, contentType);
        }

        [Obsolete, MapToApiVersion("1.0")]
        [HttpGet("{id:long}/{versionId}/{templateCode:int}/{width:int}x{height:int}")]
        [ProducesResponseType(typeof(byte[]), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetV10(long id, string versionId, int templateCode, int width, int height)
        {
            var (imageStream, contentType) = await _imagePreviewService.GetRoundedPreview(id, versionId, templateCode, width, height);
            return new FileStreamResult(imageStream, contentType);
        }
    }
}
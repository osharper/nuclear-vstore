using System;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Http.Core.Controllers;
using NuClear.VStore.Objects;
using NuClear.VStore.Options;

namespace NuClear.VStore.Renderer.Controllers
{
    [ApiVersion("1.0")]
    [Route("api/{api-version:apiVersion}/raws")]
    [Route("raws")]
    public sealed class RawsController : VStoreController
    {
        private readonly Uri _fileStorageEndpoint;
        private readonly ObjectsStorageReader _objectsStorageReader;

        public RawsController(VStoreOptions vStoreOptions, ObjectsStorageReader objectsStorageReader)
        {
            _fileStorageEndpoint = vStoreOptions.FileStorageEndpoint;
            _objectsStorageReader = objectsStorageReader;
        }

        [HttpGet("{raw}")]
        [ProducesResponseType(302)]
        [ProducesResponseType(404)]
        public IActionResult RedirectToRaw(string raw) => Redirect(new Uri(_fileStorageEndpoint, raw).ToString());

        [HttpGet("{id:long}/{versionId}/{templateCode:int}")]
        [ProducesResponseType(302)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> RedirectToRaw(long id, string versionId, int templateCode)
        {
            var descriptor = await _objectsStorageReader.GetObjectDescriptor(id, versionId);
            if (!(descriptor.Elements.Where(x => x.TemplateCode == templateCode).Select(x => x.Value).SingleOrDefault() is IImageElementValue value))
            {
                return NotFound();
            }

            return Redirect(new Uri(_fileStorageEndpoint, value.Raw).ToString());
        }
    }
}
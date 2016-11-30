using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Host.Extensions;
using NuClear.VStore.Objects;
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
        public async Task<JsonResult> GetTemplateDescriptor(long id, string versionId)
        {
            var descriptor = await _objectStorageReader.GetTemplateDescriptor(id, versionId);
            return Json(descriptor);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(long id)
        {
            try
            {
                var descriptor = await _objectStorageReader.GetObjectDescriptor(id, null);
                return Json(descriptor);
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
                return Json(descriptor);
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
        public async Task<IActionResult> Create(long id,  [FromBody]IObjectDescriptor objectDescriptor)
        {
            var versionId = ""; //await _objectManagementService.Create(id, objectDescriptor);
            var url = Url.AbsoluteAction("Get", "Object", new { id, versionId });
            return Created(url, versionId);
        }
    }
}
using System;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Host.ActionResults;

using NoContentResult = NuClear.VStore.Host.ActionResults.NoContentResult;

namespace NuClear.VStore.Host.Controllers
{
    public abstract class VStoreController : ControllerBase
    {
        [NonAction]
        public NoContentResult NoContent(string location)
        {
            return new NoContentResult(location);
        }

        [NonAction]
        public virtual JsonResult Json(object data)
        {
            return new JsonResult(data);
        }

        [NonAction]
        public ConflictResult Conflict()
        {
            return new ConflictResult();
        }

        [NonAction]
        public BadRequestObjectResult BadRequest(Exception exception)
        {
            return new BadRequestObjectResult(new { exception.Message });
        }

        [NonAction]
        public PreconditionFailedResult PreconditionFailed()
        {
            return new PreconditionFailedResult();
        }

        [NonAction]
        public UnprocessableResult Unprocessable(object value)
        {
            return new UnprocessableResult(value);
        }

        [NonAction]
        public GoneResult Gone(DateTime expiresAt)
        {
            return new GoneResult(expiresAt);
        }

        [NonAction]
        public StatusCodeResult NotModified()
        {
            return new NotModifiedResult();
        }

        [NonAction]
        public StatusCodeResult InternalServerError()
        {
            return new StatusCodeResult(500);
        }

        protected void SetResponseHeaders(IVersioned versionedObject)
        {
            Response.Headers[HeaderNames.ETag] = versionedObject.VersionId;
            Response.Headers[HeaderNames.LastModified] = versionedObject.LastModified.ToString("R");
        }
    }
}
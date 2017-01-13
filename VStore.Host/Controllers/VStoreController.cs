using System;

using Microsoft.AspNetCore.Mvc;

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
        public StatusCodeResult InternalServerError()
        {
            return new StatusCodeResult(500);
        }
    }
}
using System;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Host.ActionResults;
using NuClear.VStore.Http;

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
        public ConflictResult Conflict(string message)
        {
            return new ConflictResult(message) { ContentType = ContentType.PlainText };
        }

        [NonAction]
        public PreconditionFailedResult PreconditionFailed()
        {
            return new PreconditionFailedResult();
        }

        [NonAction]
        public UnprocessableResult Unprocessable(JToken value)
        {
            return new UnprocessableResult(value) { ContentTypes = new MediaTypeCollection { ContentType.Json } };
        }

        [NonAction]
        public UnprocessableResult Unprocessable(string value)
        {
            return new UnprocessableResult(value) { ContentTypes = new MediaTypeCollection { ContentType.PlainText } };
        }

        [NonAction]
        public GoneResult Gone(DateTime expiresAt)
        {
            return new GoneResult(expiresAt);
        }

        [NonAction]
        public NotModifiedResult NotModified()
        {
            return new NotModifiedResult();
        }

        [NonAction]
        public BadRequestContentResult BadRequest(string message)
        {
            return new BadRequestContentResult(message) { ContentType = ContentType.PlainText };
        }

        [NonAction]
        public RequestTooLargeContentResult RequestTooLarge(string message)
        {
            return new RequestTooLargeContentResult(message) { ContentType = ContentType.PlainText };
        }
    }
}
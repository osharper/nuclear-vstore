using System;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Http.Core.ActionResults;

using NoContentResult = NuClear.VStore.Http.Core.ActionResults.NoContentResult;

namespace NuClear.VStore.Http.Core.Controllers
{
    public abstract class VStoreController : ControllerBase
    {
        [NonAction]
        public NoContentResult NoContent(string location) => new NoContentResult(location);

        [NonAction]
        public virtual JsonResult Json(object data) => new JsonResult(data);

        [NonAction]
        public ConflictResult Conflict(string message) => new ConflictResult(message) { ContentType = ContentType.PlainText };

        [NonAction]
        public PreconditionFailedResult PreconditionFailed() => new PreconditionFailedResult();

        [NonAction]
        public UnprocessableResult Unprocessable(JToken value)
            => new UnprocessableResult(value) { ContentTypes = new MediaTypeCollection { ContentType.Json } };

        [NonAction]
        public UnprocessableResult Unprocessable(string value)
            => new UnprocessableResult(value) { ContentTypes = new MediaTypeCollection { ContentType.PlainText } };

        [NonAction]
        public GoneResult Gone(DateTime expiresAt) => new GoneResult(expiresAt);

        [NonAction]
        public NotModifiedResult NotModified() => new NotModifiedResult();

        [NonAction]
        public BadRequestContentResult BadRequest(string message)
            => new BadRequestContentResult(message) { ContentType = ContentType.PlainText };

        [NonAction]
        public RequestTooLargeContentResult RequestTooLarge(string message)
            => new RequestTooLargeContentResult(message) { ContentType = ContentType.PlainText };

        [NonAction]
        public ServiceUnavailableResult ServiceUnavailable(string message)
            => new ServiceUnavailableResult(message) { ContentType = ContentType.PlainText };
    }
}
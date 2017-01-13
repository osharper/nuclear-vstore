using Microsoft.AspNetCore.Mvc;

using NuClear.VStore.Host.ActionResults;

namespace NuClear.VStore.Host.Controllers
{
    public abstract class VStoreController : ControllerBase
    {
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
    }
}
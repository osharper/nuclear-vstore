using Microsoft.AspNetCore.Mvc;

namespace NuClear.VStore.Host.ActionResults
{
    public sealed class NoContentResult : StatusCodeResult
    {
        private readonly string _location;

        public NoContentResult(string location) : base(204)
        {
            _location = location;
        }

        public override void ExecuteResult(ActionContext context)
        {
            base.ExecuteResult(context);
            context.HttpContext.Response.Headers["Location"] = _location;
        }
    }
}
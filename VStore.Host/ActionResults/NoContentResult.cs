using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

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
            context.HttpContext.Response.Headers[HeaderNames.Location] = _location;
        }
    }
}
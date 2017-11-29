using System;

using Microsoft.AspNetCore.Mvc;

namespace NuClear.VStore.Http.Core.ActionResults
{
    public sealed class GoneResult : StatusCodeResult
    {
        public GoneResult(DateTime expiresAt) : base(410)
        {
            ExpiresAt = expiresAt;
        }

        public DateTime ExpiresAt { get; }

        public override void ExecuteResult(ActionContext context)
        {
            base.ExecuteResult(context);
            context.HttpContext.Response.Headers["Expires"] = ExpiresAt.ToString("R");
        }
    }
}
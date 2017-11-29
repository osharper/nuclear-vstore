using Microsoft.AspNetCore.Mvc;

namespace NuClear.VStore.Http.Core.ActionResults
{
    public sealed class NotModifiedResult : StatusCodeResult
    {
        public NotModifiedResult() : base(304)
        {
        }
    }
}
using Microsoft.AspNetCore.Mvc;

namespace NuClear.VStore.Host.ActionResults
{
    public sealed class NotModifiedResult : StatusCodeResult
    {
        public NotModifiedResult() : base(304)
        {
        }
    }
}
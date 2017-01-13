using Microsoft.AspNetCore.Mvc;

namespace NuClear.VStore.Host.ActionResults
{
    public sealed class ConflictResult : StatusCodeResult
    {
        public ConflictResult() : base(409)
        {
        }
    }
}
using Microsoft.AspNetCore.Mvc;

namespace NuClear.VStore.Host.ActionResults
{
    public sealed class PreconditionFailedResult : StatusCodeResult
    {
        public PreconditionFailedResult() : base(412)
        {
        }
    }
}
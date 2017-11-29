using Microsoft.AspNetCore.Mvc;

namespace NuClear.VStore.Http.Core.ActionResults
{
    public sealed class PreconditionFailedResult : StatusCodeResult
    {
        public PreconditionFailedResult() : base(412)
        {
        }
    }
}
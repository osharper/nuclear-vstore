using Microsoft.AspNetCore.Mvc;

namespace NuClear.VStore.Http.Core.ActionResults
{
    public sealed class UnprocessableResult : ObjectResult
    {
        public UnprocessableResult(object value) : base(value)
        {
            StatusCode = 422;
        }
    }
}

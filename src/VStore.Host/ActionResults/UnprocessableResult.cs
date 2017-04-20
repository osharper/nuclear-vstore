using Microsoft.AspNetCore.Mvc;

namespace NuClear.VStore.Host.ActionResults
{
    public sealed class UnprocessableResult : ObjectResult
    {
        public UnprocessableResult(object value) : base(value)
        {
            StatusCode = 422;
        }
    }
}

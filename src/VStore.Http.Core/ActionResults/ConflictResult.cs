using Microsoft.AspNetCore.Mvc;

namespace NuClear.VStore.Http.Core.ActionResults
{
    public sealed class ConflictResult : ContentResult
    {
        public ConflictResult(string message)
        {
            StatusCode = 409;
            Content = message;
        }
    }
}

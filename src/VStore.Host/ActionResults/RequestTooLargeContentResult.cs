using Microsoft.AspNetCore.Mvc;

namespace NuClear.VStore.Host.ActionResults
{
    public class RequestTooLargeContentResult : ContentResult
    {
        public RequestTooLargeContentResult(string message)
        {
            StatusCode = 413;
            Content = message;
        }
    }
}

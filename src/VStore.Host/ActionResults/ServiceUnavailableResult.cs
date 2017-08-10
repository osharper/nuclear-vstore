using Microsoft.AspNetCore.Mvc;

namespace NuClear.VStore.Host.ActionResults
{
    public sealed class ServiceUnavailableResult : ContentResult
    {
        public ServiceUnavailableResult(string message)
        {
            StatusCode = 503;
            Content = message;
        }
    }
}
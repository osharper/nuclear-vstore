using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

namespace NuClear.VStore.Http.Core.Middleware
{
    public sealed class CrosscuttingTraceIdentifierMiddleware
    {
        private readonly RequestDelegate _next;

        public CrosscuttingTraceIdentifierMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext httpContext)
        {
            if (httpContext.Request.Headers.TryGetValue(HeaderNames.RequestId, out var requestIdHeader))
            {
                httpContext.TraceIdentifier = requestIdHeader.ToString();
            }

            httpContext.Response.Headers.Add(HeaderNames.Server, "vstore");

            await _next(httpContext);
        }
    }
}
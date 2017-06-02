using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

using NuClear.VStore.Http;

namespace NuClear.VStore.Host.Middleware
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
            if (httpContext.Request.Headers.TryGetValue(HeaderNames.RequestId, out StringValues requestIdHeader))
            {
                httpContext.TraceIdentifier = requestIdHeader.ToString();
            }

            await _next(httpContext);
        }
    }
}
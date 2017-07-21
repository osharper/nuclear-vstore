using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;

using Serilog.Context;

namespace NuClear.VStore.Host.Middleware
{
    public sealed class RequestHeadersLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public RequestHeadersLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            using (LogContext.PushProperty("RequestHeaders", string.Join(", ", context.Request.Headers.Select(x => $"{x.Key}: {x.Value}")), destructureObjects: true))
            {
                await _next.Invoke(context);
            }
        }
    }
}
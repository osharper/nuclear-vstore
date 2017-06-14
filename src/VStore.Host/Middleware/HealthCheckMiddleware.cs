using System;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace NuClear.VStore.Host.Middleware
{
    public sealed class HealthCheckMiddleware
    {
        private const string Path = "/healthcheck";

        private readonly RequestDelegate _next;

        public HealthCheckMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            if (!context.Request.Path.Equals(Path, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
            }
            else
            {
                context.Response.ContentType = "text/plain";
                context.Response.StatusCode = 200;
                context.Response.Headers.Add(HeaderNames.Connection, "close");
                await context.Response.WriteAsync("OK");
            }
        }
    }
}

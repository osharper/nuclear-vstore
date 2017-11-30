using System.IO;
using System.Linq;

using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NuClear.VStore.Http.Core.Swashbuckle
{
    public sealed class ViewFileFilter : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            if (context.ApiDescription.SupportedResponseTypes.Any(x => x.Type == typeof(byte[])))
            {
                operation.Produces = new[] { "application/octet-stream" };
            }
        }
    }
}
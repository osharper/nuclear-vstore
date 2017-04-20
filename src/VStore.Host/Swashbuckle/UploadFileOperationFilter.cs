using System.Collections.Generic;
using System.Linq;

using NuClear.VStore.Host.Filters;

using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace NuClear.VStore.Host.Swashbuckle
{
    public class UploadFileOperationFilter : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            var multipartBodyFilters = context.ApiDescription.ActionDescriptor.FilterDescriptors
                                              .Select(x => x.Filter)
                                              .OfType<MultipartBodyLengthLimitAttribute>()
                                              .ToArray();

            if (multipartBodyFilters.Length == 0)
            {
                return;
            }

            var fileParam = new NonBodyParameter
                                {
                                    Type = "file",
                                    Name = "file",
                                    In = "formData"
                                };
            operation.Parameters.Add(fileParam);
            operation.Consumes = new List<string> { "multipart/form-data" };
        }
    }
}
using Swashbuckle.Swagger.Model;
using Swashbuckle.SwaggerGen.Generator;

namespace NuClear.VStore.Host.Swashbuckle
{
    public class UploadFileOperationFilter : IOperationFilter
    {
        public void Apply(Operation operation, OperationFilterContext context)
        {
            // controller and action name
            if (operation.OperationId == "Api1.0ContentByIdByElementIdPut")
            {
                if (operation.Parameters.Count > 0)
                {
                    var firstParam = operation.Parameters[0];
                    operation.Parameters.Clear();
                    operation.Parameters.Add(firstParam);
                }

                operation.Parameters.Add(
                             new NonBodyParameter
                                 {
                                     Name = "File",
                                     In = "formData",
                                     Description = "Upload Image",
                                     Required = true,
                                     Type = "file"
                                 });
                operation.Consumes.Add("application/form-data");
            }
        }
    }
}
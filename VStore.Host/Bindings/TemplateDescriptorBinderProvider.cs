using Microsoft.AspNetCore.Mvc.ModelBinding;

using NuClear.VStore.Host.Descriptors;

namespace NuClear.VStore.Host.Bindings
{
    public sealed class TemplateDescriptorBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            return context.Metadata.ModelType == typeof(TemplateDescriptor) ? new TemplateDescriptorBinder() : null;
        }
    }
}
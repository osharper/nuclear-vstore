using System;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using NuClear.VStore.Options;

namespace NuClear.VStore.Host.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class MultipartBodyLengthLimitAttribute : Attribute, IAuthorizationFilter, IOrderedFilter
    {
        private static FormOptions _formOptions;

        public int Order { get; set; }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            if (_formOptions == null)
            {
                var options = context.HttpContext.RequestServices.GetService<IOptions<VStoreOptions>>();
                _formOptions = new FormOptions
                    {
                        MultipartBodyLengthLimit = options.Value.MaxBinarySize
                    };
            }

            var features = context.HttpContext.Features;
            var formFeature = features.Get<IFormFeature>();

            if (formFeature?.Form == null)
            {
                // Request form has not been read yet, so set the limits
                features.Set<IFormFeature>(new FormFeature(context.HttpContext.Request, _formOptions));
            }
        }
    }
}
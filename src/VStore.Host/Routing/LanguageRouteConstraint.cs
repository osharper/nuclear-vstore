using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using NuClear.VStore.Descriptors;

namespace NuClear.VStore.Host.Routing
{
    public class LanguageRouteConstraint : IRouteConstraint
    {
        private static readonly IReadOnlyList<string> LangNames = Enum.GetNames(typeof(Language));

        public bool Match(HttpContext httpContext, IRouter route, string routeKey, RouteValueDictionary values, RouteDirection routeDirection)
        {
            if (httpContext == null)
            {
                throw new ArgumentNullException(nameof(httpContext));
            }

            if (route == null)
            {
                throw new ArgumentNullException(nameof(route));
            }

            if (routeKey == null)
            {
                throw new ArgumentNullException(nameof(routeKey));
            }

            if (values == null)
            {
                throw new ArgumentNullException(nameof(values));
            }

            if (values.TryGetValue(routeKey, out var value) && value != null)
            {
                if (value is Language)
                {
                    return Enum.IsDefined(typeof(Language), value);
                }

                var valueString = Convert.ToString(value, CultureInfo.InvariantCulture);
                return LangNames.Contains(valueString, StringComparer.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}

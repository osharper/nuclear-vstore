using System;
using System.Globalization;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using NuClear.VStore.Descriptors;

namespace NuClear.VStore.Host.Routing
{
    public class LanguageRouteConstraint : IRouteConstraint
    {
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

            if (values.TryGetValue(routeKey, out object value) && value != null)
            {
                if (value is Language)
                {
                    return Enum.IsDefined(typeof(Language), value);
                }

                var valueString = Convert.ToString(value, CultureInfo.InvariantCulture);
                return Enum.TryParse(valueString, true, out Language language)
                       && Enum.IsDefined(typeof(Language), language)
                       && language.ToString().Equals(valueString, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}

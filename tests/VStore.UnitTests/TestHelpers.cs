using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Moq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Errors;

using Xunit;

namespace VStore.UnitTests
{
    internal static class TestHelpers
    {
        internal delegate IEnumerable<ObjectElementValidationError> Validator(IObjectElementValue value, IElementConstraints elementConstraints);

        internal static TError MakeValidationCheck<TValue, TError>(TValue value, IElementConstraints constraints, Validator validator, Action<TValue> valueChanger)
            where TValue : IObjectElementValue
            where TError : ObjectElementValidationError
        {
            Assert.Empty(validator(value, constraints));
            valueChanger(value);

            var errors = validator(value, constraints).ToList();
            Assert.Single(errors);
            Assert.IsType<TError>(errors.First());

            return (TError)errors.First();
        }

        internal static void InternalTextChecksTest(
            IEnumerable<Validator> allChecks,
            bool containsRestrictedSymbols,
            int expectedErrorsCount,
            IObjectElementValue value,
            TextElementConstraints constraints)
        {
            var errors = new List<ObjectElementValidationError>();
            foreach (var validator in allChecks)
            {
                errors.AddRange(validator(value, constraints));
            }

            Assert.Equal(expectedErrorsCount, errors.Count);
            if (containsRestrictedSymbols)
            {
                Assert.Single(errors.OfType<NonBreakingSpaceSymbolError>());
                Assert.Single(errors.OfType<ControlCharactersInTextError>());
            }

            if (constraints.MaxSymbols.HasValue)
            {
                Assert.Single(errors.OfType<ElementTextTooLongError>());
            }

            if (constraints.MaxLines.HasValue)
            {
                Assert.Single(errors.OfType<TooManyLinesError>());
            }

            if (constraints.MaxSymbolsPerWord.HasValue)
            {
                Assert.Single(errors.OfType<ElementWordsTooLongError>());
            }
        }

        internal static bool TestRouteConstraint(IRouteConstraint constraint, object value)
        {
            var route = new RouteCollection();
            var context = new Mock<HttpContext>();

            const string ParameterName = "fake";
            var values = new RouteValueDictionary { { ParameterName, value } };
            return constraint.Match(context.Object, route, ParameterName, values, RouteDirection.IncomingRequest);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

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
            Assert.Equal(1, errors.Count);
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
                Assert.Equal(1, errors.OfType<NonBreakingSpaceSymbolError>().Count());
                Assert.Equal(1, errors.OfType<ControlСharactersInTextError>().Count());
            }

            if (constraints.MaxSymbols.HasValue)
            {
                Assert.Equal(1, errors.OfType<ElementTextTooLongError>().Count());
            }

            if (constraints.MaxLines.HasValue)
            {
                Assert.Equal(1, errors.OfType<TooManyLinesError>().Count());
            }

            if (constraints.MaxSymbolsPerWord.HasValue)
            {
                Assert.Equal(1, errors.OfType<ElementWordsTooLongError>().Count());
            }
        }
    }
}

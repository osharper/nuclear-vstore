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

        internal static TException MakeCheck<TValue, TException>(TValue value, IElementConstraints constraints, Validator validator, Action<TValue> valueChanger)
            where TValue : IObjectElementValue
            where TException : ObjectElementValidationError
        {
            Assert.Empty(validator(value, constraints));
            valueChanger(value);

            var errors = validator(value, constraints).ToList();
            Assert.StrictEqual(1, errors.Count);
            Assert.IsType<TException>(errors.First());

            return (TException)errors.First();
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

            Assert.StrictEqual(expectedErrorsCount, errors.Count);
            if (containsRestrictedSymbols)
            {
                Assert.Contains(errors, err => err.GetType() == typeof(NonBreakingSpaceSymbolError));
                Assert.Contains(errors, err => err.GetType() == typeof(ControlСharactersInTextError));
            }

            if (constraints.MaxSymbols.HasValue)
            {
                Assert.Contains(errors, err => err.GetType() == typeof(ElementTextTooLongError));
            }

            if (constraints.MaxLines.HasValue)
            {
                Assert.Contains(errors, err => err.GetType() == typeof(TooManyLinesError));
            }

            if (constraints.MaxSymbolsPerWord.HasValue)
            {
                Assert.Contains(errors, err => err.GetType() == typeof(ElementWordsTooLongError));
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Exceptions;

using Xunit;

namespace VStore.UnitTests
{
    internal static class TestHelpers
    {
        internal delegate IEnumerable<Exception> Validator(IObjectElementValue value, IElementConstraints elementConstraints);

        internal static TException MakeCheck<TValue, TException>(TValue value, IElementConstraints constraints, Validator validator, Action<TValue> valueChanger)
            where TValue : IObjectElementValue
            where TException : Exception
        {
            Assert.Empty(validator(value, constraints));
            valueChanger(value);

            var errors = validator(value, constraints).ToList();
            Assert.StrictEqual(1, errors.Count);
            Assert.IsType<TException>(errors.First());

            return (TException)errors.First();
        }

        internal static void InternalChecksTest(
            IEnumerable<Validator> allChecks,
            bool containsRestrictedSymbols,
            int expectedErrorsCount,
            IObjectElementValue value,
            TextElementConstraints constraints)
        {
            var errors = new List<Exception>();
            foreach (var validator in allChecks)
            {
                errors.AddRange(validator(value, constraints));
            }

            Assert.StrictEqual(expectedErrorsCount, errors.Count);
            if (containsRestrictedSymbols)
            {
                Assert.Contains(errors, err => err.GetType() == typeof(NonBreakingSpaceSymbolException));
                Assert.Contains(errors, err => err.GetType() == typeof(ControlСharactersInTextException));
            }

            if (constraints.MaxSymbols.HasValue)
            {
                Assert.Contains(errors, err => err.GetType() == typeof(ElementTextTooLongException));
            }

            if (constraints.MaxLines.HasValue)
            {
                Assert.Contains(errors, err => err.GetType() == typeof(TooManyLinesException));
            }

            if (constraints.MaxSymbolsPerWord.HasValue)
            {
                Assert.Contains(errors, err => err.GetType() == typeof(ElementWordsTooLongException));
            }
        }
    }
}

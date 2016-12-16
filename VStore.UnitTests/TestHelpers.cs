using System;
using System.Collections.Generic;
using System.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Errors;

using NUnit.Framework;

namespace VStore.UnitTests
{
    internal static class TestHelpers
    {
        internal delegate IEnumerable<ObjectElementValidationError> Validator(IObjectElementValue value, IElementConstraints elementConstraints);

        internal static TException MakeCheck<TValue, TException>(TValue value, IElementConstraints constraints, Validator validator, Action<TValue> valueChanger)
            where TValue : IObjectElementValue
            where TException : ObjectElementValidationError
        {
            Assert.IsEmpty(validator(value, constraints));
            valueChanger(value);

            var errors = validator(value, constraints).ToList();
            Assert.AreEqual(1, errors.Count);
            Assert.IsInstanceOf<TException>(errors.First());

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

            Assert.AreEqual(expectedErrorsCount, errors.Count);
            if (containsRestrictedSymbols)
            {
                Assert.That(errors, Has.Exactly(1).InstanceOf<NonBreakingSpaceSymbolError>());
                Assert.That(errors, Has.Exactly(1).InstanceOf<ControlСharactersInTextError>());
            }

            if (constraints.MaxSymbols.HasValue)
            {
                Assert.That(errors, Has.Exactly(1).InstanceOf<ElementTextTooLongError>());
            }

            if (constraints.MaxLines.HasValue)
            {
                Assert.That(errors, Has.Exactly(1).InstanceOf<TooManyLinesError>());
            }

            if (constraints.MaxSymbolsPerWord.HasValue)
            {
                Assert.That(errors, Has.Exactly(1).InstanceOf<ElementWordsTooLongError>());
            }
        }
    }
}

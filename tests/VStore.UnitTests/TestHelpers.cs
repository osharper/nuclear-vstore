using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

using Moq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Errors;
using NuClear.VStore.Sessions;
using NuClear.VStore.Sessions.ContentValidation.Errors;

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

        internal static TError MakeBinaryValidationCheck<TError>(string content, Action<int, Stream> testAction, string expectedErrorType, int templateCode = 1)
            where TError : BinaryValidationError
        {
            InvalidBinaryException ex;
            using (var stream = new MemoryStream())
            {
                using (var sw = new StreamWriter(stream, Encoding.ASCII))
                {
                    sw.Write(content);
                    sw.Flush();
                    stream.Position = 0;
                    ex = Assert.Throws<InvalidBinaryException>(() => testAction(templateCode, stream));
                }
            }

            Assert.IsType<TError>(ex.Error);
            Assert.Equal(templateCode, ex.TemplateCode);
            Assert.Equal(expectedErrorType, ex.Error.ErrorType);
            return (TError)ex.Error;
        }

        public static void MakeBinaryValidationCheck(string content, Action<int, Stream> testAction, int templateCode = 1)
        {
            using (var stream = new MemoryStream())
            {
                using (var sw = new StreamWriter(stream, Encoding.ASCII))
                {
                    sw.Write(content);
                    sw.Flush();
                    stream.Position = 0;
                    testAction(templateCode, stream);
                }
            }
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
                Assert.Equal(1, errors.OfType<ControlCharactersInTextError>().Count());
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

using System;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.Errors;

using Xunit;

namespace VStore.UnitTests.Validation
{
    public class DateValidationTests
    {
        [Fact]
        public void TestDateRangeValidation()
        {
            var value = new DateElementValue { BeginDate = DateTime.Today, EndDate = DateTime.Today.AddDays(1) };
            var constraints = new DateElementConstraints();

            var error = TestHelpers.MakeValidationCheck<DateElementValue, InvalidDateRangeError>(
                value,
                constraints,
                DateValidator.CheckDate,
                val => val.EndDate = val.BeginDate?.AddMinutes(-1));
            Assert.Equal(ElementConstraintViolations.ValidDateRange, error.ErrorType);
        }

        [Fact]
        public void TestNullsInDateRangeValidation()
        {
            var value = new DateElementValue { BeginDate = null, EndDate = null };
            var constraints = new DateElementConstraints();

            var error = TestHelpers.MakeValidationCheck<DateElementValue, InvalidDateRangeError>(
                value,
                constraints,
                DateValidator.CheckDate,
                val => val.EndDate = DateTime.Now);
            Assert.Equal(ElementConstraintViolations.ValidDateRange, error.ErrorType);

            var now = DateTime.UtcNow;
            value = new DateElementValue { BeginDate = now, EndDate = now };

            error = TestHelpers.MakeValidationCheck<DateElementValue, InvalidDateRangeError>(
                value,
                constraints,
                DateValidator.CheckDate,
                val => val.BeginDate = null);
            Assert.Equal(ElementConstraintViolations.ValidDateRange, error.ErrorType);
        }
    }
}

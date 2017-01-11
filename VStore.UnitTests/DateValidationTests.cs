using System;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.Errors;

using Xunit;

namespace VStore.UnitTests
{
    public class DateValidationTests
    {
        [Fact]
        public void TestDateRangeValidation()
        {
            var value = new DateElementValue { BeginDate = DateTime.Today, EndDate = DateTime.Today.AddDays(1) };

            var error = TestHelpers.MakeCheck<DateElementValue, InvalidDateRangeError>(
                value,
                null,
                DateValidator.CheckDate,
                val => val.EndDate = DateTime.Today.AddMinutes(-1));
            Assert.Equal(ElementValidationErrors.InvalidDateRange, error.ErrorType);
        }
    }
}

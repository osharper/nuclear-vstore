using System;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Objects.ContentValidation;
using NuClear.VStore.Objects.ContentValidation.Errors;

using NUnit.Framework;

namespace VStore.UnitTests
{
    [TestFixture]
    public class DateValidationTests
    {
        [Test]
        public void TestDateRangeValidation()
        {
            var value = new DateElementValue { BeginDate = DateTime.Today, EndDate = DateTime.Today.AddDays(1) };

            var error = TestHelpers.MakeCheck<DateElementValue, InvalidDateRangeError>(
                value,
                null,
                DateValidator.CheckDate,
                val => val.EndDate = DateTime.Today.AddMinutes(-1));
            Assert.AreEqual(ElementValidationErrors.InvalidDateRange, error.ErrorType);
        }
    }
}

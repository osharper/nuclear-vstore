using System;
using System.Collections.Generic;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Exceptions;

namespace NuClear.VStore.Objects.ContentValidation
{
    public static class DateValidator
    {
        public static IEnumerable<ObjectElementValidationException> CheckDate(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var dateValue = (DateElementValue)value;

            var errors = new List<ObjectElementValidationException>();
            if (dateValue.BeginDate > dateValue.EndDate)
            {
                errors.Add(new InvalidDateRangeException());
            }

            var constraints = (DateElementConstraints)elementConstraints;
            var datesDifference = dateValue.EndDate - dateValue.BeginDate;
            if (constraints.MinDays.HasValue &&
                datesDifference < TimeSpan.FromDays(constraints.MinDays.Value))
            {
                errors.Add(new IncorrectPeriodException(constraints.MinDays.Value, datesDifference, isExceeds: false));
            }

            if (constraints.MaxDays.HasValue &&
                datesDifference > TimeSpan.FromDays(constraints.MaxDays.Value))
            {
                errors.Add(new IncorrectPeriodException(constraints.MaxDays.Value, datesDifference, isExceeds: true));
            }

            return errors;
        }
    }
}

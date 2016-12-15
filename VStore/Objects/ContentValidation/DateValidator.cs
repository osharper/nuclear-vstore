using System;
using System.Collections.Generic;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Errors;

namespace NuClear.VStore.Objects.ContentValidation
{
    public static class DateValidator
    {
        public static IEnumerable<ObjectElementValidationError> CheckDate(IObjectElementValue value, IElementConstraints elementConstraints)
        {
            var dateValue = (DateElementValue)value;

            return dateValue.BeginDate > dateValue.EndDate
                ? new[] { new InvalidDateRangeError() }
                : Array.Empty<ObjectElementValidationError>();
        }
    }
}

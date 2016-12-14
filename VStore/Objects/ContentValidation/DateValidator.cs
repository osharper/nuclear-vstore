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

            return dateValue.BeginDate > dateValue.EndDate
                ? new[] { new InvalidDateRangeException() }
                : Array.Empty<ObjectElementValidationException>();
        }
    }
}

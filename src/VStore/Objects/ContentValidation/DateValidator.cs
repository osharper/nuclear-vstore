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

            var beginDate = dateValue.BeginDate;
            var endDate = dateValue.EndDate;
            var bothDatesAreNull = beginDate == null && endDate == null;

            // Дата начала не должна быть больше даты окончания, либо обе даты не заданы:
            return bothDatesAreNull || beginDate <= endDate
                       ? Array.Empty<ObjectElementValidationError>()
                       : new[] { new InvalidDateRangeError() };
        }
    }
}

using System;

namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class IncorrectPeriodException : ObjectElementValidationException
    {
        public IncorrectPeriodException(int allowedDaysCount, TimeSpan datesDifference, bool isExceeds)
        {
            AllowedDaysCount = allowedDaysCount;
            DatesDifference = datesDifference;
            IsExceeds = isExceeds;
        }

        public int AllowedDaysCount { get; }

        public TimeSpan DatesDifference { get; }

        public bool IsExceeds { get; }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.IncorrectPeriod;
    }
}

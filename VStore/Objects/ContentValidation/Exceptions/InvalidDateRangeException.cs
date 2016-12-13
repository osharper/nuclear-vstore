namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class InvalidDateRangeException : ObjectElementValidationException
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.InvalidDateRange;
    }
}

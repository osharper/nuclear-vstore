namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class InvalidDateRangeError : ObjectElementValidationError
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.InvalidDateRange;
    }
}

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class InvalidDateRangeError : ObjectElementValidationError
    {
        public override ElementConstraintViolations ErrorType => ElementConstraintViolations.ValidDateRange;
    }
}

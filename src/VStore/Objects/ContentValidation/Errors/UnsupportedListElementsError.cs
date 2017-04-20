namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class UnsupportedListElementsError : ObjectElementValidationError
    {
        public override ElementConstraintViolations ErrorType => ElementConstraintViolations.SupportedListElements;
    }
}

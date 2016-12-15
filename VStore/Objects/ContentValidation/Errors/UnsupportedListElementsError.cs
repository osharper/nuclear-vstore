namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class UnsupportedListElementsError : ObjectElementValidationError
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.UnsupportedListElements;
    }
}

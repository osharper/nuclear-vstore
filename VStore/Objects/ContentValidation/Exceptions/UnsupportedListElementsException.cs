namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class UnsupportedListElementsException : ObjectElementValidationException
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.UnsupportedListElements;
    }
}

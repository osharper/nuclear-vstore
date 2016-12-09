namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class UnsupportedListElementsException : ObjectElementValidationException
    {
        public UnsupportedListElementsException() : base("Unsupported list element found")
        {
        }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.UnsupportedListElements;
    }
}

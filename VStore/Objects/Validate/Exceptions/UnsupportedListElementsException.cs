namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class UnsupportedListElementsException : ObjectElementValidationException
    {
        public UnsupportedListElementsException() : base("Unsupported list element found")
        {
        }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.UnsupportedListElements;
    }
}

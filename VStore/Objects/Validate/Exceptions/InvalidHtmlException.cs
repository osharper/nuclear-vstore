namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class InvalidHtmlException : ObjectElementValidationException
    {
        public InvalidHtmlException() : base("Html is invalid")
        {
        }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.InvalidHtml;
    }
}

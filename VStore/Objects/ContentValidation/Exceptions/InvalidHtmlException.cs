namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class InvalidHtmlException : ObjectElementValidationException
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.InvalidHtml;
    }
}

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class InvalidHtmlError : ObjectElementValidationError
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.InvalidHtml;
    }
}

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class InvalidHtmlError : ObjectElementValidationError
    {
        public override ElementConstraintViolations ErrorType => ElementConstraintViolations.ValidHtml;
    }
}

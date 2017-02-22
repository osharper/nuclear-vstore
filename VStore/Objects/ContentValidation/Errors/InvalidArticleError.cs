namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class InvalidArticleError : ObjectElementValidationError
    {
        public override ElementConstraintViolations ErrorType => ElementConstraintViolations.ValidArticle;
    }
}

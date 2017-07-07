namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class InvalidArticleError : BinaryValidationError
    {
        public override BinaryConstraintViolations ErrorType => BinaryConstraintViolations.ValidArticle;
    }
}

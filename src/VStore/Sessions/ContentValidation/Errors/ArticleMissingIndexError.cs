namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class ArticleMissingIndexError : BinaryValidationError
    {
        public override BinaryConstraintViolations ErrorType => BinaryConstraintViolations.ContainsIndexFile;
    }
}

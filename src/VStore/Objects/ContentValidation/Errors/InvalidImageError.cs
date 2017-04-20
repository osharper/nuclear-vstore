namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class InvalidImageError : ObjectElementValidationError
    {
        public override ElementConstraintViolations ErrorType => ElementConstraintViolations.ValidImage;
    }
}

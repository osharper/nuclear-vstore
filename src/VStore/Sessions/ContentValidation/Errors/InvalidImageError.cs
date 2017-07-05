namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class InvalidImageError : BinaryValidationError
    {
        public override BinaryConstraintViolations ErrorType => BinaryConstraintViolations.ValidImage;
    }
}

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class ImageAlphaChannelError : BinaryValidationError
    {
        public override BinaryConstraintViolations ErrorType => BinaryConstraintViolations.IsAlphaChannelRequired;
    }
}

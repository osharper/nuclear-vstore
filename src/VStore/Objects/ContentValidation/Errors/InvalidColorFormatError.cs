namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public sealed class InvalidColorFormatError : ObjectElementValidationError
    {
        public override ElementConstraintViolations ErrorType => ElementConstraintViolations.ValidColor;
    }
}
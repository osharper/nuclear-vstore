namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class ControlCharactersInTextError : ObjectElementValidationError
    {
        public override ElementConstraintViolations ErrorType => ElementConstraintViolations.WithoutControlChars;
    }
}

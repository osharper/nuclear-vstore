namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class ControlСharactersInTextError : ObjectElementValidationError
    {
        public override ElementConstraintViolations ErrorType => ElementConstraintViolations.WithoutControlСhars;
    }
}

namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class ControlСharactersInTextException : ObjectElementValidationException
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.ControlСharacters;
    }
}

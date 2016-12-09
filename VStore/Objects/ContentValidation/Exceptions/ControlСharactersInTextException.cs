namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class ControlСharactersInTextException : ObjectElementValidationException
    {
        public ControlСharactersInTextException() : base("Control characters found")
        {
        }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.ControlСharacters;
    }
}

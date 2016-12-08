namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class ControlСharactersInTextException : ObjectElementValidationException
    {
        public ControlСharactersInTextException() : base("Control characters found")
        {
        }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.ControlСharactersInText;
    }
}

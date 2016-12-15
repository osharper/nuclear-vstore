namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public sealed class IncorrectLinkError : ObjectElementValidationError
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.IncorrectLink;
    }
}

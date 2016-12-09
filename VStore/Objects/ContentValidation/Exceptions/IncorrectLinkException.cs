namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public sealed class IncorrectLinkException : ObjectElementValidationException
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.IncorrectLink;
    }
}

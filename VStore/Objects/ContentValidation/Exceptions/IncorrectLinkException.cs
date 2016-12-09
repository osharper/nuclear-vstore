namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public sealed class IncorrectLinkException : ObjectElementValidationException
    {
        public IncorrectLinkException() : base("Link is incorrect")
        {
        }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.IncorrectLink;
    }
}

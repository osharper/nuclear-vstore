namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class NonBreakingSpaceSymbolException : ObjectElementValidationException
    {
        public NonBreakingSpaceSymbolException() : base("Non-breaking space found")
        {
        }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.NonBreakingSpaceSymbol;
    }
}

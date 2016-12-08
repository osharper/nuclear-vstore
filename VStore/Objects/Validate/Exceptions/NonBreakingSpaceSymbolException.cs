namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class NonBreakingSpaceSymbolException : ObjectElementValidationException
    {
        public NonBreakingSpaceSymbolException() : base("Non-breaking space found")
        {
        }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.NonBreakingSpaceSymbol;
    }
}

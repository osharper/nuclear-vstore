namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class NonBreakingSpaceSymbolException : ObjectElementValidationException
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.NonBreakingSpaceSymbol;
    }
}

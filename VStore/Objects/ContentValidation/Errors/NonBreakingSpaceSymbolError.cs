namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class NonBreakingSpaceSymbolError : ObjectElementValidationError
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.NonBreakingSpaceSymbol;
    }
}

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class NonBreakingSpaceSymbolError : ObjectElementValidationError
    {
        public override ElementConstraintViolations ErrorType => ElementConstraintViolations.WithoutNonBreakingSpace;
    }
}

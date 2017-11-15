using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class NonBreakingSpaceSymbolError : ObjectElementValidationError
    {
        public override string ErrorType => nameof(ITextElementConstraints.WithoutNonBreakingSpace);
    }
}

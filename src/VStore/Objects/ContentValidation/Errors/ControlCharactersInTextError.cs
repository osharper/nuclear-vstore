using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class ControlCharactersInTextError : ObjectElementValidationError
    {
        public override string ErrorType => nameof(ITextElementConstraints.WithoutControlChars);
    }
}

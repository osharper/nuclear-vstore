using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public sealed class InvalidColorFormatError : ObjectElementValidationError
    {
        public override string ErrorType => nameof(ColorElementConstraints.ValidColor);
    }
}
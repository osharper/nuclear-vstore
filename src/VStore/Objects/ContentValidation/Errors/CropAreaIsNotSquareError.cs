using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public sealed class CropAreaIsNotSquareError : ObjectElementValidationError
    {
        public override string ErrorType => nameof(LogoElementConstraints.CropAreaIsSquare);
    }
}
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class CropAreaNotConsistentWithCropShapeError : ObjectElementValidationError
    {
        public override string ErrorType => nameof(LogoElementConstraints.CropAreaConsistentWithCropShape);
    }
}
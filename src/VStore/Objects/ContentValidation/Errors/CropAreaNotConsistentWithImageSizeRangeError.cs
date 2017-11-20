using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class CropAreaNotConsistentWithImageSizeRangeError : ObjectElementValidationError
    {
        public CropAreaNotConsistentWithImageSizeRangeError(ImageSizeRange imageSizeRange, ImageSize cropSize)
        {
            ImageSizeRange = imageSizeRange;
            CropSize = cropSize;
        }

        public override string ErrorType => nameof(LogoElementConstraints.CropAreaConsistentWithImageSizeRange);

        public ImageSizeRange ImageSizeRange { get; }
        public ImageSize CropSize { get; }
    }
}
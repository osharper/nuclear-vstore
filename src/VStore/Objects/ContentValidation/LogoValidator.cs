using System;
using System.Collections.Generic;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Errors;

namespace NuClear.VStore.Objects.ContentValidation
{
    public static class LogoValidator
    {
        public static IEnumerable<ObjectElementValidationError> CheckValidLogo(IObjectElementValue value, IElementConstraints constraints)
        {
            var logoValue = (ILogoElementValue)value;
            var logoConstraints = (LogoElementConstraints)constraints;

            var cropSize = new ImageSize { Width = Math.Abs(logoValue.CropArea.X1 - logoValue.CropArea.X2), Height = Math.Abs(logoValue.CropArea.Y1 - logoValue.CropArea.Y2) };
            if (logoConstraints.CropAreaIsSquare && cropSize.Width != cropSize.Height)
            {
                return new[] { new CropAreaIsNotSquareError() };
            }

            if (logoConstraints.CropAreaConsistentWithImageSizeRange && !logoConstraints.ImageSizeRange.Includes(cropSize))
            {
                return new[] { new CropAreaNotConsistentWithImageSizeRangeError(logoConstraints.ImageSizeRange, cropSize) };
            }

            return Array.Empty<ObjectElementValidationError>();
        }
    }
}
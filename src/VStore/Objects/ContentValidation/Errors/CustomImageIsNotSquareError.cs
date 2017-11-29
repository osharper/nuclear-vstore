using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Sessions.ContentValidation.Errors;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class CustomImageIsNotSquareError : BinaryValidationError
    {
        public override string ErrorType => nameof(LogoElementConstraints.CustomImageIsSquare);
    }
}
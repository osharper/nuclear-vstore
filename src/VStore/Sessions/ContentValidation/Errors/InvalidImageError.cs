using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class InvalidImageError : BinaryValidationError
    {
        public override string ErrorType => nameof(IImageElementConstraints.ValidImage);
    }
}

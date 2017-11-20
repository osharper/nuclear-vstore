using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class ImageHasAlphaChannelError : BinaryValidationError
    {
        public override string ErrorType => nameof(LogoElementConstraints.AlphaChannelNotAllowed);
    }
}
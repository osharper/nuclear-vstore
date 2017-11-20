using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class CustomImageMissingAlphaChannelError : BinaryValidationError
    {
        public override string ErrorType => nameof(LogoElementConstraints.CustomImageAlphaChannelRequired);
    }
}
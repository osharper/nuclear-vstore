using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class ImageAlphaChannelError : BinaryValidationError
    {
        public override string ErrorType => nameof(BitmapImageElementConstraints.IsAlphaChannelRequired);
    }
}

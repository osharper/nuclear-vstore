using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class ImageWithUrlInStylesError : BinaryValidationError
    {
        public override string ErrorType => nameof(VectorImageElementConstraints.WithoutUrlInStyles);
    }
}

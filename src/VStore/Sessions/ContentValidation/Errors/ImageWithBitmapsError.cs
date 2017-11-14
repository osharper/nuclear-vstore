using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class ImageWithBitmapsError : BinaryValidationError
    {
        public override string ErrorType => nameof(VectorImageElementConstraints.WithoutBitmaps);
    }
}

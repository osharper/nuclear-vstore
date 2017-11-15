using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class ImageWithUnclosedPathsError : BinaryValidationError
    {
        public override string ErrorType => nameof(VectorImageElementConstraints.PathsAreClosed);
    }
}

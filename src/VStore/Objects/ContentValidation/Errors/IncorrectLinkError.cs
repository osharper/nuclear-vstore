using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public sealed class IncorrectLinkError : ObjectElementValidationError
    {
        public override string ErrorType => nameof(ILinkElementConstraints.ValidLink);
    }
}

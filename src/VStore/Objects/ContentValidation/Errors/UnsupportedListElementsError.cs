using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class UnsupportedListElementsError : ObjectElementValidationError
    {
        public override string ErrorType => nameof(FormattedTextElementConstraints.SupportedListElements);
    }
}

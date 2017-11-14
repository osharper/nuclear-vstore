using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class InvalidHtmlError : ObjectElementValidationError
    {
        public override string ErrorType => nameof(FormattedTextElementConstraints.ValidHtml);
    }
}

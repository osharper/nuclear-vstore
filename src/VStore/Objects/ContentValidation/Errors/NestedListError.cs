using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class NestedListError : ObjectElementValidationError
    {
        public override string ErrorType => nameof(FormattedTextElementConstraints.NoNestedLists);
    }
}

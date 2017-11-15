using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class EmptyListError : ObjectElementValidationError
    {
        public override string ErrorType => nameof(FormattedTextElementConstraints.NoEmptyLists);
    }
}

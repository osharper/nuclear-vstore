namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class NestedListError : ObjectElementValidationError
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.NestedList;
    }
}

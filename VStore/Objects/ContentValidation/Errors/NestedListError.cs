namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class NestedListError : ObjectElementValidationError
    {
        public override ElementConstraintViolations ErrorType => ElementConstraintViolations.NoNestedLists;
    }
}

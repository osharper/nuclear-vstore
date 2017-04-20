namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class EmptyListError : ObjectElementValidationError
    {
        public override ElementConstraintViolations ErrorType => ElementConstraintViolations.NoEmptyLists;
    }
}

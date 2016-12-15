namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class EmptyListError : ObjectElementValidationError
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.EmptyList;
    }
}

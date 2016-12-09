namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class NestedListException : ObjectElementValidationException
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.NestedList;
    }
}

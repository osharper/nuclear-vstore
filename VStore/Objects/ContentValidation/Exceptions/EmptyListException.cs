namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class EmptyListException : ObjectElementValidationException
    {
        public override ElementValidationErrors ErrorType => ElementValidationErrors.EmptyList;
    }
}

namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class EmptyListException : ObjectElementValidationException
    {
        public EmptyListException() : base("Empty list found")
        {
        }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.EmptyList;
    }
}

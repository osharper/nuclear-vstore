namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class EmptyListException : ObjectElementValidationException
    {
        public EmptyListException() : base("Empty list found")
        {
        }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.EmptyList;
    }
}

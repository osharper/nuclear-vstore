namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class NestedListException : ObjectElementValidationException
    {
        public NestedListException() : base("Nested list found")
        {
        }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.NestedList;
    }
}

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class ElementTextTooLongException : ObjectElementValidationException
    {
        public ElementTextTooLongException(int maxLength, int actualLength) :
            base($"Text length {actualLength} exceeds the maximum {maxLength}")
        {
            MaxLength = maxLength;
            ActualLength = actualLength;
        }

        public int MaxLength { get; }

        public int ActualLength { get; }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.TextTooLong;
    }
}

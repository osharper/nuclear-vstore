namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class TooManyLinesException : ObjectElementValidationException
    {
        public TooManyLinesException(int maxLinesCount, int actualLinesCount) :
            base($"Found lines count {actualLinesCount} exceeds maximum: {maxLinesCount}")
        {
            MaxLinesCount = maxLinesCount;
            ActualLinesCount = actualLinesCount;
        }

        public int MaxLinesCount { get; }

        public int ActualLinesCount { get; }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.TooManyLines;
    }
}

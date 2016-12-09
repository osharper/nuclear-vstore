namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class TooManyLinesException : ObjectElementValidationException
    {
        public TooManyLinesException(int maxLinesCount, int actualLinesCount)
        {
            MaxLinesCount = maxLinesCount;
            ActualLinesCount = actualLinesCount;
        }

        public int MaxLinesCount { get; }

        public int ActualLinesCount { get; }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.TooManyLines;
    }
}

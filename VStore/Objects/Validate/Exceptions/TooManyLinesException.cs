using System;

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class TooManyLinesException : Exception
    {
        public TooManyLinesException(int maxLinesCount, int actualLinesCount) :
            base($"Found lines count {actualLinesCount} exceeds maximum: {maxLinesCount}")
        {
        }
    }
}

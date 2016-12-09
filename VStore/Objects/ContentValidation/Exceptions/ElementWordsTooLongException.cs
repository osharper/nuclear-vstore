using System.Collections.Generic;

namespace NuClear.VStore.Objects.ContentValidation.Exceptions
{
    public class ElementWordsTooLongException : ObjectElementValidationException
    {
        public ElementWordsTooLongException(int maxWordLength, IReadOnlyCollection<string> tooLongWords)
        {
            MaxWordLength = maxWordLength;
            TooLongWords = tooLongWords;
        }

        public int MaxWordLength { get; }

        public IReadOnlyCollection<string> TooLongWords { get; }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.WordsTooLong;
    }
}

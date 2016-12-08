using System.Collections.Generic;

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class ElementWordsTooLongException : ObjectElementValidationException
    {
        public ElementWordsTooLongException(int maxWordLength, IReadOnlyCollection<string> tooLongWords) :
            base($"Found words that exceed maximum length of {maxWordLength}: {string.Join(", ", tooLongWords)}")
        {
            MaxWordLength = maxWordLength;
            TooLongWords = tooLongWords;
        }

        public int MaxWordLength { get; }

        public IReadOnlyCollection<string> TooLongWords { get; }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.WordsTooLong;
    }
}

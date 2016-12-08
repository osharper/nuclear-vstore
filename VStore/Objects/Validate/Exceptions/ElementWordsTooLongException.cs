using System;
using System.Collections.Generic;

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class ElementWordsTooLongException : Exception
    {
        public ElementWordsTooLongException(int maxWordLength, IReadOnlyCollection<string> tooLongWords) :
            base($"Found words that exceed maximum length of {maxWordLength}: {string.Join(", ", tooLongWords)}")
        {
            MaxWordLength = maxWordLength;
            TooLongWords = tooLongWords;
        }

        public int MaxWordLength { get; }

        public IReadOnlyCollection<string> TooLongWords { get; }
    }
}

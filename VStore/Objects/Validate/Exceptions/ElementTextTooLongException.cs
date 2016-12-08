using System;

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class ElementTextTooLongException : Exception
    {
        public ElementTextTooLongException(int maxLength, int actualLength) :
            base($"Text length {actualLength} exceeds the maximum {maxLength}")
        {
            MaxLength = maxLength;
            ActualLength = actualLength;
        }

        public int MaxLength { get; set; }

        public int ActualLength { get; set; }
    }
}

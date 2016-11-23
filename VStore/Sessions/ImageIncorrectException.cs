using System;

namespace NuClear.VStore.Sessions
{
    public sealed class ImageIncorrectException : Exception
    {
        public ImageIncorrectException(string message) : base(message)
        {
        }

        public ImageIncorrectException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
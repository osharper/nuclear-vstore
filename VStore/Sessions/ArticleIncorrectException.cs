using System;

namespace NuClear.VStore.Sessions
{
    public sealed class ArticleIncorrectException : Exception
    {
        public ArticleIncorrectException(string message) : base(message)
        {
        }

        public ArticleIncorrectException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}

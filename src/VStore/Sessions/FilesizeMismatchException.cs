using System;

namespace NuClear.VStore.Sessions
{
    public sealed class FilesizeMismatchException : Exception
    {
        public FilesizeMismatchException(string message) : base(message)
        {
        }
    }
}
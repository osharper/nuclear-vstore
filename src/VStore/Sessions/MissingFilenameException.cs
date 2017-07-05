using System;

namespace NuClear.VStore.Sessions
{
    public class MissingFilenameException : Exception
    {
        public MissingFilenameException(string message) : base(message)
        {
        }
    }
}

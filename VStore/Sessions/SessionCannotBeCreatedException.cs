using System;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionCannotBeCreatedException : Exception
    {
        public SessionCannotBeCreatedException(string message) : base(message)
        {
        }
    }
}
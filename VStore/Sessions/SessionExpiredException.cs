using System;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionExpiredException : Exception
    {
        public SessionExpiredException(Guid sessionId) : base($"Session {sessionId} expired.")
        {
        }
    }
}
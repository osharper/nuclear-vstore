using System;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionExpiredException : Exception
    {
        public SessionExpiredException(Guid sessionId, DateTime expiredAt)
            : base($"Session '{sessionId}' has expired at '{expiredAt}'.")
        {
            ExpiredAt = expiredAt;
        }

        public DateTime ExpiredAt { get; }
    }
}
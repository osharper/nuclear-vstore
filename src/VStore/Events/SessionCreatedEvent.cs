using System;

namespace NuClear.VStore.Events
{
    public class SessionCreatedEvent : IEvent
    {
        public SessionCreatedEvent(Guid sessionId, DateTime expiresAt)
        {
            SessionId = sessionId;
            ExpiresAt = expiresAt;
        }

        public string Key => SessionId.ToString();
        public Guid SessionId { get; }
        public DateTime ExpiresAt { get; }
    }
}
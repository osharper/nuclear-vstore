using System;

namespace NuClear.VStore.Events
{
    public class SessionCreatedEvent : IEvent
    {
        public Guid SessionId { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
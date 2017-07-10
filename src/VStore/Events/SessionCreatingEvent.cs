using System;

using Newtonsoft.Json;

using NuClear.VStore.Json;

namespace NuClear.VStore.Events
{
    public sealed class SessionCreatingEvent : IEvent
    {
        public SessionCreatingEvent(Guid sessionId, DateTime expiresAt)
        {
            SessionId = sessionId;
            ExpiresAt = expiresAt;
        }

        public string Key => SessionId.ToString();
        public Guid SessionId { get; }
        public DateTime ExpiresAt { get; }

        public string Serialize() => JsonConvert.SerializeObject(new { SessionId, ExpiresAt }, SerializerSettings.Default);
    }
}
using System;

namespace NuClear.VStore.Descriptors.Sessions
{
    public sealed class SessionContext
    {
        public SessionContext(SessionDescriptor descriptor, string author, DateTime expiresAt)
        {
            Descriptor = descriptor;
            Author = author;
            ExpiresAt = expiresAt;
        }

        public SessionDescriptor Descriptor { get; }
        public string Author { get; }
        public DateTime ExpiresAt { get; }
    }
}
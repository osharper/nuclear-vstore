using System;

using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors.Sessions
{
    public sealed class SessionContext
    {
        public SessionContext(long templateId, IVersionedTemplateDescriptor templateDescriptor, string author, DateTime expiresAt)
        {
            TemplateId = templateId;
            TemplateDescriptor = templateDescriptor;
            Author = author;
            ExpiresAt = expiresAt;
        }

        public long TemplateId { get; }
        public IVersionedTemplateDescriptor TemplateDescriptor { get; }
        public string Author { get; }
        public DateTime ExpiresAt { get; }
    }
}
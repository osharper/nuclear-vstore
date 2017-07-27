using System;

using NuClear.VStore.DataContract;
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors.Sessions
{
    public sealed class SessionContext
    {
        public SessionContext(
            long templateId,
            IVersionedTemplateDescriptor templateDescriptor,
            Language language,
            AuthorInfo authorInfo,
            DateTime expiresAt)
        {
            TemplateId = templateId;
            TemplateDescriptor = templateDescriptor;
            Language = language;
            AuthorInfo = authorInfo;
            ExpiresAt = expiresAt;
        }

        public long TemplateId { get; }
        public IVersionedTemplateDescriptor TemplateDescriptor { get; }
        public Language Language { get; }
        public AuthorInfo AuthorInfo { get; }
        public DateTime ExpiresAt { get; }
    }
}
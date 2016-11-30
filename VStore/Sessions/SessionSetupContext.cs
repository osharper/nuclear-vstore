using System;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionSetupContext : IIdentifyable<Guid>
    {
        public SessionSetupContext(IVersionedTemplateDescriptor templateDescriptor)
        {
            Id = Guid.NewGuid();
            TemplateDescriptor = templateDescriptor;
            ExpiresAt = CurrentTime().AddDays(1);
        }

        public Guid Id { get; }
        public IVersionedTemplateDescriptor TemplateDescriptor { get; }
        public DateTime ExpiresAt { get; }

        public static bool IsSessionExpired(DateTime expiresAt)
        {
            return expiresAt <= CurrentTime();
        }

        private static DateTime CurrentTime() => DateTime.UtcNow;
    }
}
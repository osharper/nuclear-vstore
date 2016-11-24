using System;
using System.Collections.Generic;
using System.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Sessions;
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionSetupContext : IDescriptor, IIdentifyable<Guid>
    {
        private const string RouteToken = "session";

        public SessionSetupContext(Uri endpointUri, TemplateDescriptor templateDescriptor)
        {
            Id = Guid.NewGuid();
            TemplateDescriptor = templateDescriptor;
            UploadUris = templateDescriptor.GetBinaryElementTemplateCodes()
                                           .Select(x => new Uri(endpointUri, $"{RouteToken}/{Id}/{templateDescriptor.Id}/{templateDescriptor.VersionId}/{x}"))
                                           .ToArray();
            ExpiresAt = CurrentTime().AddDays(1);
        }

        public Guid Id { get; }
        public TemplateDescriptor TemplateDescriptor { get; }
        public IReadOnlyCollection<Uri> UploadUris { get; }
        public DateTime ExpiresAt { get; }

        public static bool IsSessionExpired(DateTime expiresAt)
        {
            return expiresAt <= CurrentTime();
        }

        private static DateTime CurrentTime() => DateTime.UtcNow;
    }
}
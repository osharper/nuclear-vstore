using System;
using System.Collections.Generic;
using System.Linq;

using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors.Sessions
{
    public sealed class SessionDescriptor : IDescriptor, IIdentifyable<Guid>
    {
        private const string RouteToken = "session";

        public SessionDescriptor(Uri endpointUri, TemplateDescriptor templateDescriptor)
        {
            Id = Guid.NewGuid();
            TemplateDescriptor = templateDescriptor;

            var templateCodes = TemplateDescriptor.Elements
                                                  .Where(x => x.Type == ElementDescriptorType.Article || x.Type == ElementDescriptorType.Image)
                                                  .Select(x => x.TemplateCode)
                                                  .ToArray();
            UploadUris = templateCodes.Select(x => new Uri(endpointUri, $"{RouteToken}/{Id}/{templateDescriptor.Id}/{templateDescriptor.VersionId}/{x}")).ToArray();
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
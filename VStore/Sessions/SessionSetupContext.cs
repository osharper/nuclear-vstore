using System;
using System.Collections.Generic;
using System.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Sessions;
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionSetupContext : IIdentifyable<Guid>
    {
        private const string RouteToken = "session";

        public SessionSetupContext(Uri endpointUri, TemplateDescriptor templateDescriptor)
        {
            Id = Guid.NewGuid();
            TemplateDescriptor = templateDescriptor;
            UploadUris = templateDescriptor.GetBinaryElementTemplateCodes()
                                           .Select(x => new UploadUri(x, new Uri(endpointUri, $"{RouteToken}/{Id}/{templateDescriptor.Id}/{templateDescriptor.VersionId}/{x}")))
                                           .ToArray();
            ExpiresAt = CurrentTime().AddDays(1);
        }

        public Guid Id { get; }
        public TemplateDescriptor TemplateDescriptor { get; }
        public IReadOnlyCollection<UploadUri> UploadUris { get; }
        public DateTime ExpiresAt { get; }

        public static bool IsSessionExpired(DateTime expiresAt)
        {
            return expiresAt <= CurrentTime();
        }

        private static DateTime CurrentTime() => DateTime.UtcNow;

        public sealed class UploadUri
        {
            public UploadUri(int templateCode, Uri uri)
            {
                TemplateCode = templateCode;
                Uri = uri;
            }

            public int TemplateCode { get; }
            public Uri Uri { get; }
        }
    }
}
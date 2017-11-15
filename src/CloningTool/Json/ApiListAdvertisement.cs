using System;

using NuClear.VStore.Descriptors;

namespace CloningTool.Json
{
    public class ApiListAdvertisement
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public string VersionId { get; set; }

        public Language Language { get; set; }

        public bool IsWhiteListed { get; set; }

        public DateTime CreatedAt { get; set; }

        public FirmDescriptor Firm { get; set; }

        public AdvertisementTemplate Template { get; set; }

        public ModerationResult Moderation { get; set; }

        public override string ToString() => $"Id = {Id.ToString()}, Name = {Name}";

        public class AdvertisementTemplate
        {
            public long Id { get; set; }

            public string Name { get; set; }
        }
    }
}

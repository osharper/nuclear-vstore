using System;

namespace CloningTool
{
    public sealed class CloningToolOptions
    {
        public CloneMode Mode { get; set; }

        public bool FetchAdvertisementBeforeClone { get; set; }

        public bool OverwriteUnequalTemplates { get; set; } = false;

        public int MaxDegreeOfParallelism { get; set; }

        public int MaxCloneTries { get; set; } = 3;

        public int TruncatedCloneSize { get; set; } = 5;

        public DateTime? AdvertisementsCreatedAtBeginDate { get; set; }

        public long? AdvertisementsTemplateId { get; set; }

        public string SourceApiToken { get; set; }

        public string DestApiToken { get; set; }

        public string ApiVersion { get; set; }

        public int InitialPingTries { get; set; }

        public int InitialPingInterval { get; set; }
    }
}

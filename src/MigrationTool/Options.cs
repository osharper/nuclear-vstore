using System;

namespace MigrationTool
{
    public sealed class Options
    {
        public ImportMode Mode { get; set; }

        public int MaxDegreeOfParallelism { get; set; }

        public int MaxImportTries { get; set; } = 3;

        public int AdvertisementsImportBatchSize { get; set; } = 1000;

        public int TruncatedImportSize { get; set; } = 5;

        public DateTime ThresholdDate { get; set; }

        public DateTime PositionsBeginDate { get; set; }

        public string ApiToken { get; set; }

        public int InitialPingTries { get; set; }

        public int InitialPingInterval { get; set; }

        public int? DestOrganizationUnitBranchCode { get; set; }
    }
}

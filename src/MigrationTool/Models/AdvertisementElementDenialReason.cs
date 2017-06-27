using System;

namespace MigrationTool.Models
{
    public sealed class AdvertisementElementDenialReason
    {
        public long Id { get; set; }
        public long AdvertisementElementId { get; set; }
        public long DenialReasonId { get; set; }
        public string Comment { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public AdvertisementElement AdvertisementElement { get; set; }
        public DenialReason DenialReason { get; set; }
    }
}

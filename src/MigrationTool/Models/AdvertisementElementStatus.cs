using System;

namespace MigrationTool.Models
{
    public sealed class AdvertisementElementStatus
    {
        public long Id { get; set; }
        public int Status { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public AdvertisementElement AdvertisementElement { get; set; }
    }
}

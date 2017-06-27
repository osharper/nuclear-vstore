using System;
using System.Collections.Generic;

namespace MigrationTool.Models
{
    public sealed class DenialReason
    {
        public DenialReason()
        {
            AdvertisementElementDenialReasons = new HashSet<AdvertisementElementDenialReason>();
        }

        public long Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string ProofLink { get; set; }
        public int Type { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public bool IsActive { get; set; }

        public ICollection<AdvertisementElementDenialReason> AdvertisementElementDenialReasons { get; set; }
    }
}

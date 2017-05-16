using System;
using System.Collections.Generic;

namespace MigrationTool.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Advertisement
    {
        public Advertisement()
        {
            AdvertisementElements = new HashSet<AdvertisementElement>();
            OrderPositionAdvertisement = new HashSet<OrderPositionAdvertisement>();
        }

        public long Id { get; set; }
        public long? FirmId { get; set; }
        public long AdvertisementTemplateId { get; set; }
        public bool IsSelectedToWhiteList { get; set; }
        public string Name { get; set; }
        public string Comment { get; set; }
        public bool IsDeleted { get; set; }
        public long OwnerCode { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? DgppId { get; set; }

        public ICollection<AdvertisementElement> AdvertisementElements { get; set; }
        public ICollection<OrderPositionAdvertisement> OrderPositionAdvertisement { get; set; }
        public AdvertisementTemplate AdvertisementTemplate { get; set; }
    }
}

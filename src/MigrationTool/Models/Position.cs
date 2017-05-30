using System.Collections.Generic;

namespace MigrationTool.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Position
    {
        public Position()
        {
            OrderPositionAdvertisement = new HashSet<OrderPositionAdvertisement>();
            PricePositions = new HashSet<PricePosition>();
            PositionChildrenChildPosition = new HashSet<PositionChildren>();
            PositionChildrenMasterPosition = new HashSet<PositionChildren>();
        }

        public long Id { get; set; }
        public string Name { get; set; }
        public bool IsComposite { get; set; }
        public long? AdvertisementTemplateId { get; set; }
        public bool IsContentSales { get; set; }
        public bool IsDeleted { get; set; }

        public ICollection<OrderPositionAdvertisement> OrderPositionAdvertisement { get; set; }
        public ICollection<PricePosition> PricePositions { get; set; }
        public ICollection<PositionChildren> PositionChildrenChildPosition { get; set; }
        public ICollection<PositionChildren> PositionChildrenMasterPosition { get; set; }
        public AdvertisementTemplate AdvertisementTemplate { get; set; }
    }
}

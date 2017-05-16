using System;
using System.Collections.Generic;

namespace MigrationTool.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class OrderPosition
    {
        public OrderPosition()
        {
            OrderPositionAdvertisement = new HashSet<OrderPositionAdvertisement>();
        }

        public long Id { get; set; }
        public long OrderId { get; set; }
        public long? DgppId { get; set; }
        public Guid ReplicationCode { get; set; }
        public long PricePositionId { get; set; }
        public string Extensions { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public ICollection<OrderPositionAdvertisement> OrderPositionAdvertisement { get; set; }
        public Order Order { get; set; }
        public PricePosition PricePosition { get; set; }
    }
}

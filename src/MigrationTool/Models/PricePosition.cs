using System;
using System.Collections.Generic;

namespace MigrationTool.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class PricePosition
    {
        public PricePosition()
        {
            OrderPositions = new HashSet<OrderPosition>();
        }

        public long Id { get; set; }
        public long? DgppId { get; set; }
        public long PriceId { get; set; }
        public long PositionId { get; set; }
        public decimal Cost { get; set; }
        public int? MinAdvertisementAmount { get; set; }
        public int? MaxAdvertisementAmount { get; set; }
        public int? Amount { get; set; }
        public int AmountSpecificationMode { get; set; }
        public int RateType { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsActive { get; set; }
        public long OwnerCode { get; set; }
        public long CreatedBy { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public Position Position { get; set; }
        public Price Price { get; set; }
        public ICollection<OrderPosition> OrderPositions { get; set; }
    }
}

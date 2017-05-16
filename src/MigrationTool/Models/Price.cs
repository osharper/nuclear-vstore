using System;
using System.Collections.Generic;

namespace MigrationTool.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class Price
    {
        public Price()
        {
            PricePositions = new HashSet<PricePosition>();
        }

        public long Id { get; set; }
        public long? DgppId { get; set; }
        public DateTime CreateDate { get; set; }
        public DateTime PublishDate { get; set; }
        public DateTime BeginDate { get; set; }
        public bool IsPublished { get; set; }
        public long OrganizationUnitId { get; set; }
        public long CurrencyId { get; set; }
        public bool IsDeleted { get; set; }
        public bool IsActive { get; set; }
        public long CreatedBy { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public ICollection<PricePosition> PricePositions { get; set; }
    }
}

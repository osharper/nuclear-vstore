using System;

namespace MigrationTool.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class OrderPositionAdvertisement
    {
        public long Id { get; set; }
        public long OrderPositionId { get; set; }
        public long PositionId { get; set; }
        public long? AdvertisementId { get; set; }
        public long? FirmAddressId { get; set; }
        public long? CategoryId { get; set; }
        public long? ThemeId { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public Advertisement Advertisement { get; set; }
        public OrderPosition OrderPosition { get; set; }
        public Position Position { get; set; }
    }
}

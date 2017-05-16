using System;
using System.Collections.Generic;

namespace MigrationTool.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class AdvertisementTemplate
    {
        public AdvertisementTemplate()
        {
            ElementTemplatesLink = new HashSet<ElementTemplateLink>();
            Advertisements = new HashSet<Advertisement>();
            Positions = new HashSet<Position>();
        }

        public long Id { get; set; }
        public long? DummyAdvertisementId { get; set; }
        public string Name { get; set; }
        public string Comment { get; set; }
        public bool IsPublished { get; set; }
        public bool IsAllowedToWhiteList { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public bool IsAdvertisementRequired { get; set; }

        public ICollection<ElementTemplateLink> ElementTemplatesLink { get; set; }
        public ICollection<Advertisement> Advertisements { get; set; }
        public ICollection<Position> Positions { get; set; }
    }
}

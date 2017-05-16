using System;
using System.Collections.Generic;

namespace MigrationTool.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class ElementTemplateLink
    {
        public ElementTemplateLink()
        {
            Elements = new HashSet<AdvertisementElement>();
        }

        public long Id { get; set; }
        public long AdsTemplateId { get; set; }
        public long AdsElementTemplateId { get; set; }
        public int ExportCode { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public ICollection<AdvertisementElement> Elements { get; set; }
        public AdvertisementElementTemplate ElementTemplate { get; set; }
        public AdvertisementTemplate Template { get; set; }
    }
}

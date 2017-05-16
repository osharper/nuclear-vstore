using System;
using System.Collections.Generic;

namespace MigrationTool.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class AdvertisementElementTemplate
    {
        public AdvertisementElementTemplate()
        {
            AdsTemplatesAdsElementTemplates = new HashSet<ElementTemplateLink>();
            AdvertisementElements = new HashSet<AdvertisementElement>();
        }

        public long Id { get; set; }
        public string Name { get; set; }
        public ElementRestrictionType RestrictionType { get; set; }
        public int? TextLengthRestriction { get; set; }
        public byte? MaxSymbolsInWord { get; set; }
        public int? TextLineBreaksCountRestriction { get; set; }
        public bool FormattedText { get; set; }
        public int? FileSizeRestriction { get; set; }
        public string FileExtensionRestriction { get; set; }
        public int? FileNameLengthRestriction { get; set; }
        public string ImageDimensionRestriction { get; set; }
        public bool IsRequired { get; set; }
        public bool IsAlphaChannelRequired { get; set; }
        public bool NeedsValidation { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public bool IsAdvertisementLink { get; set; }
        public bool IsPhoneNumber { get; set; }

        public ICollection<ElementTemplateLink> AdsTemplatesAdsElementTemplates { get; set; }
        public ICollection<AdvertisementElement> AdvertisementElements { get; set; }
    }
}

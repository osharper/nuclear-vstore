using System;

namespace MigrationTool.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class AdvertisementElement
    {
        public long Id { get; set; }
        public long AdvertisementId { get; set; }
        public long AdvertisementElementTemplateId { get; set; }
        public long AdsTemplatesAdsElementTemplatesId { get; set; }
        public string Text { get; set; }
        public long? FileId { get; set; }
        public DateTime? BeginDate { get; set; }
        public DateTime? EndDate { get; set; }
        public FasComment? FasCommentType { get; set; }
        public bool IsDeleted { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? DgppId { get; set; }

        public ElementTemplateLink AdsTemplatesAdsElementTemplates { get; set; }
        public AdvertisementElementTemplate AdvertisementElementTemplate { get; set; }
        public Advertisement Advertisement { get; set; }
        public File File { get; set; }
    }
}

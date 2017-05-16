using System;
using System.Collections.Generic;

namespace MigrationTool.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class File
    {
        public File()
        {
            AdvertisementElements = new HashSet<AdvertisementElement>();
        }

        public long Id { get; set; }
        public string FileName { get; set; }
        public string ContentType { get; set; }
        public long ContentLength { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public long? DgppId { get; set; }
        public byte[] Data { get; set; }

        public ICollection<AdvertisementElement> AdvertisementElements { get; set; }
    }
}

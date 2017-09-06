using System;

namespace MigrationTool.Models
{
    public sealed class Note
    {
        public long Id { get; set; }
        public long ParentId { get; set; }
        public int ParentType { get; set; }
        public string Title { get; set; }
        public string Text { get; set; }
        public long? FileId { get; set; }
        public bool IsDeleted { get; set; }
        public long OwnerCode { get; set; }
        public long CreatedBy { get; set; }
        public long? ModifiedBy { get; set; }
        public DateTime CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public File File { get; set; }
    }
}

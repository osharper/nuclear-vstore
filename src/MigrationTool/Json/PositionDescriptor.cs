namespace MigrationTool.Json
{
    public class PositionDescriptor
    {
        public long Id { get; set; }

        public long? TemplateId { get; set; }

        public string Name { get; set; }

        public bool IsDeleted { get; set; }

        public bool IsContentSales { get; set; }
    }
}

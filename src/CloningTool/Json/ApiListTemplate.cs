namespace CloningTool.Json
{
    public class ApiListTemplate
    {
        public long Id { get; set; }

        public string Name { get; set; }

        public string VersionId { get; set; }

        public bool IsWhiteListed { get; set; }

        public bool IsDeleted { get; set; }

        public override string ToString() => $"Id = {Id.ToString()}, Name = {Name}";
    }
}

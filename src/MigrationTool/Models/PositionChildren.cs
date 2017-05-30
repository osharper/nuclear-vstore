namespace MigrationTool.Models
{
    // ReSharper disable once ClassNeverInstantiated.Global
    public sealed class PositionChildren
    {
        public long MasterPositionId { get; set; }
        public long ChildPositionId { get; set; }

        public Position ChildPosition { get; set; }
        public Position MasterPosition { get; set; }
    }
}

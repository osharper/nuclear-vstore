namespace NuClear.VStore.Descriptors
{
    public sealed class ContentElementDescriptor<T> : IContentElementDescriptor
    {
        public long Id { get; set; }
        public string VersionId { get; set; }
        public T Content { get; set; }
    }
}
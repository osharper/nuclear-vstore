namespace NuClear.VStore.Descriptors.Objects
{
    public sealed class ContentElementDescriptor<T> : IContentElementDescriptor
    {
        public long Id { get; set; }
        public string VersionId { get; set; }
        public T Content { get; set; }
    }
}
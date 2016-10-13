namespace NuClear.VStore.Descriptors
{
    public interface IContentElementDescriptor
    {
        long Id { get; set; }
        string VersionId { get; set; }
    }
}
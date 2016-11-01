namespace NuClear.VStore.Descriptors.Objects
{
    public interface IContentElementDescriptor
    {
        long Id { get; set; }
        string VersionId { get; set; }
    }
}
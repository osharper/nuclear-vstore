namespace NuClear.VStore.Descriptors
{
    public interface IVersionedTemplateDescriptor : ITemplateDescriptor
    {
        string VersionId { get; set; }
    }
}
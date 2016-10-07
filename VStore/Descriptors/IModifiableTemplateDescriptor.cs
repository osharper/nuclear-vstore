namespace NuClear.VStore.Descriptors
{
    public interface IModifiableTemplateDescriptor : ITemplateDescriptor
    {
        string VersionId { get; set; }
    }
}
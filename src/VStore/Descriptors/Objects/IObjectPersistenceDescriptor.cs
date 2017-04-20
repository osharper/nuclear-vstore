using Newtonsoft.Json.Linq;

namespace NuClear.VStore.Descriptors.Objects
{
    public interface IObjectPersistenceDescriptor : IDescriptor
    {
        long TemplateId { get; }
        string TemplateVersionId { get; }
        Language Language { get; }
        JObject Properties { get; set; }
    }
}

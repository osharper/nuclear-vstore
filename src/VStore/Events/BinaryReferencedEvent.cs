using Newtonsoft.Json;

using NuClear.VStore.Json;

namespace NuClear.VStore.Events
{
    public sealed class BinaryReferencedEvent : IEvent
    {
        public BinaryReferencedEvent(long objectId, string objectVersionId, int elementTemplateCode, string fileKey)
        {
            ObjectId = objectId;
            ObjectVersionId = objectVersionId;
            ElementTemplateCode = elementTemplateCode;
            FileKey = fileKey;
        }

        public string Key => ObjectId.ToString();
        public long ObjectId { get; }
        public string ObjectVersionId { get; }
        public int ElementTemplateCode { get; }
        public string FileKey { get; }

        public string Serialize() => JsonConvert.SerializeObject(
            new { ObjectId, ObjectVersionId, ElementTemplateCode, FileKey },
            SerializerSettings.Default);
    }
}
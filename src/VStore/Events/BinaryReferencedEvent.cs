using System;

using Newtonsoft.Json;

using NuClear.VStore.Json;

namespace NuClear.VStore.Events
{
    public sealed class BinaryReferencedEvent : IEvent
    {
        public BinaryReferencedEvent(long objectId, string objectVersionId, int elementTemplateCode, string fileKey, DateTime? referencedAt)
        {
            ObjectId = objectId;
            ObjectVersionId = objectVersionId;
            ElementTemplateCode = elementTemplateCode;
            FileKey = fileKey;
            ReferencedAt = referencedAt;
        }

        public string Key => FileKey;
        public long ObjectId { get; }
        public string ObjectVersionId { get; }
        public int ElementTemplateCode { get; }
        public string FileKey { get; }
        public DateTime? ReferencedAt { get; }

        public string Serialize() => JsonConvert.SerializeObject(
            new { ObjectId, ObjectVersionId, ElementTemplateCode, FileKey, ReferencedAt },
            SerializerSettings.Default);
    }
}
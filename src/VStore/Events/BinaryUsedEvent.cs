using System;

namespace NuClear.VStore.Events
{
    public sealed class BinaryUsedEvent : IEvent
    {
        public BinaryUsedEvent(long objectId, string objectVersionId, int elementTemplateCode, string fileKey)
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
    }
}
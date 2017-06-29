using System;

namespace NuClear.VStore.Events
{
    public sealed class BinaryUsedEvent : IEvent
    {
        public BinaryUsedEvent(long objectId, int elementTemplateCode, string fileKey)
        {
            ObjectId = objectId;
            ElementTemplateCode = elementTemplateCode;
            FileKey = fileKey;
        }

        public string Key => ObjectId.ToString();
        public long ObjectId { get; }
        public int ElementTemplateCode { get; }
        public string FileKey { get; }
    }
}
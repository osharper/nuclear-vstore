using System;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Json;

namespace NuClear.VStore.Events
{
    public sealed class ObjectVersionCreatedEvent : IEvent
    {
        public ObjectVersionCreatedEvent(
            long objectId,
            string versionId,
            int versionIndex,
            string author,
            JObject properties,
            DateTime lastModified)
        {
            ObjectId = objectId;
            VersionId = versionId;
            VersionIndex = versionIndex;
            Author = author;
            Properties = properties;
            LastModified = lastModified;
        }

        public string Key => ObjectId.ToString();
        public long ObjectId { get; }
        public string VersionId { get; }
        public int VersionIndex { get; }
        public string Author { get; }
        public JObject Properties { get; }
        public DateTime LastModified { get; }

        public string Serialize() => JsonConvert.SerializeObject(
            new { ObjectId, VersionId, VersionIndex, Author, Properties, LastModified },
            SerializerSettings.Default);
    }
}
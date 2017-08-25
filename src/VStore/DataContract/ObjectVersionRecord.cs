using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Objects;

namespace NuClear.VStore.DataContract
{
    public sealed class ObjectVersionRecord : IIdentifyable<long>, IVersioned
    {
        private readonly AuthorInfo _authorInfo;
        private readonly VersionedObjectDescriptor<long> _versionedObjectDescriptor;

        public ObjectVersionRecord(
            long id,
            string versionId,
            int versionIndex,
            DateTime lastModified,
            AuthorInfo authorInfo,
            JObject properties,
            IReadOnlyCollection<ElementRecord> elements,
            IReadOnlyCollection<int> modifiedElements)
        {
            _authorInfo = authorInfo;
            _versionedObjectDescriptor = new VersionedObjectDescriptor<long>(id, versionId, lastModified);
            VersionIndex = versionIndex;
            Properties = properties;
            Elements = elements;
            ModifiedElements = modifiedElements;
        }

        public long Id => _versionedObjectDescriptor.Id;
        public string VersionId => _versionedObjectDescriptor.VersionId;
        public int VersionIndex { get; }
        public string Author => _authorInfo.Author;
        public string AuthorLogin => _authorInfo.AuthorLogin;
        public string AuthorName => _authorInfo.AuthorName;
        public DateTime LastModified => _versionedObjectDescriptor.LastModified;
        public JObject Properties { get; }
        public IReadOnlyCollection<ElementRecord> Elements { get; set; }
        public IReadOnlyCollection<int> ModifiedElements { get; }

        public sealed class ElementRecord
        {
            public ElementRecord(int templateCode, IObjectElementValue value)
            {
                TemplateCode = templateCode;
                Value = value;
            }

            public int TemplateCode { get; }
            public IObjectElementValue Value { get; }
        }
    }
}
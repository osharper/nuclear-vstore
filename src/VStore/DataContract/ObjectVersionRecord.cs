using System;
using System.Collections.Generic;

using NuClear.VStore.Descriptors;

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
            IReadOnlyCollection<int> modifiedElements)
        {
            _authorInfo = authorInfo;
            _versionedObjectDescriptor = new VersionedObjectDescriptor<long>(id, versionId, lastModified);
            VersionIndex = versionIndex;
            ModifiedElements = modifiedElements;
        }

        public long Id => _versionedObjectDescriptor.Id;
        public string VersionId => _versionedObjectDescriptor.VersionId;
        public int VersionIndex { get; }
        public string Author => _authorInfo.Author;
        public string AuthorLogin => _authorInfo.AuthorLogin;
        public string AuthorName => _authorInfo.AuthorName;
        public DateTime LastModified => _versionedObjectDescriptor.LastModified;
        public IReadOnlyCollection<int> ModifiedElements { get; }
    }
}
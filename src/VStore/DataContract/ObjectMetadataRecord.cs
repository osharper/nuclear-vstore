using System;

using NuClear.VStore.Descriptors;

namespace NuClear.VStore.DataContract
{
    public sealed class ObjectMetadataRecord
    {
        private readonly AuthorInfo _authorInfo;
        private readonly VersionedObjectDescriptor<long> _versionedObjectDescriptor;

        public ObjectMetadataRecord(long id, string versionId, DateTime lastModified, AuthorInfo authorInfo)
        {
            _authorInfo = authorInfo;
            _versionedObjectDescriptor = new VersionedObjectDescriptor<long>(id, versionId, lastModified);
        }

        public long Id => _versionedObjectDescriptor.Id;
        public string VersionId => _versionedObjectDescriptor.VersionId;
        public string Author => _authorInfo.Author;
        public string AuthorLogin => _authorInfo.AuthorLogin;
        public string AuthorName => _authorInfo.AuthorName;
        public DateTime LastModified => _versionedObjectDescriptor.LastModified;
    }
}
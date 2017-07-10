using System;
using System.Collections.Generic;

namespace NuClear.VStore.Descriptors
{
    public sealed class ModifiedObjectDescriptor : IIdentifyable<long>, IVersioned
    {
        private readonly AuthorInfo _authorInfo;
        private readonly VersionedObjectDescriptor<long> _versionedObjectDescriptor;

        public ModifiedObjectDescriptor(
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
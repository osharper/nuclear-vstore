using System;
using System.Collections.Generic;

namespace NuClear.VStore.Descriptors
{
    public sealed class ModifiedObjectDescriptor : IIdentifyable<long>, IVersioned
    {
        private readonly VersionedObjectDescriptor<long> _versionedObjectDescriptor;

        public ModifiedObjectDescriptor(long id, string versionId, DateTime lastModified, IReadOnlyCollection<int> modifiedElements)
        {
            _versionedObjectDescriptor = new VersionedObjectDescriptor<long>(id, versionId, lastModified);
            ModifiedElements = modifiedElements;
        }

        public long Id => _versionedObjectDescriptor.Id;
        public string VersionId => _versionedObjectDescriptor.VersionId;
        public DateTime LastModified => _versionedObjectDescriptor.LastModified;
        public IReadOnlyCollection<int> ModifiedElements { get; }
    }
}
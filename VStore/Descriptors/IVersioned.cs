using System;

namespace NuClear.VStore.Descriptors
{
    public interface IVersioned
    {
        string VersionId { get; }
        DateTime LastModified { get; }
    }
}
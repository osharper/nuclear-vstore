using System;

namespace NuClear.VStore.S3
{
    public sealed class ConcurrencyException : Exception
    {
        public ConcurrencyException(long id, string requestedVersionId, string currentVersionId)
            : base($"VersionId '{requestedVersionId}' is outdated for the object '{id}'. Current versionId is '{currentVersionId}'")
        {
        }
    }
}
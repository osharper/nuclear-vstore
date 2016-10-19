using System;

namespace NuClear.VStore.S3
{
    public sealed class ConcurrencyException : Exception
    {
        public ConcurrencyException(string key, string requestedVersionId, string currentVersionId)
            : base($"VersionId '{requestedVersionId}' is outdated for the object '{key}'. Current versionId is '{currentVersionId}'")
        {
        }
    }
}
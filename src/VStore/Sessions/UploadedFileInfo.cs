using System;

namespace NuClear.VStore.Sessions
{
    public sealed class UploadedFileInfo
    {
        public UploadedFileInfo(string id, Uri downloadUri)
        {
            Id = id;
            DownloadUri = downloadUri;
        }

        public string Id { get; }
        public Uri DownloadUri { get; }
    }
}
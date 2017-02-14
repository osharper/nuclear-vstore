using System;

namespace NuClear.VStore.Sessions
{
    public sealed class UploadedFileInfo
    {
        public UploadedFileInfo(string id, Uri previewUri)
        {
            Id = id;
            PreviewUri = previewUri;
        }

        public string Id { get; }
        public Uri PreviewUri { get; }
    }
}
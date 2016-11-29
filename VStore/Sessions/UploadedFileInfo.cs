using System;

namespace NuClear.VStore.Sessions
{
    public sealed class UploadedFileInfo
    {
        public UploadedFileInfo(string id, string fileName, Uri previewUri)
        {
            Id = id;
            FileName = fileName;
            PreviewUri = previewUri;
        }

        public string Id { get; }
        public string FileName { get; }
        public Uri PreviewUri { get; }
    }
}
using System;

namespace NuClear.VStore.Sessions
{
    public sealed class UploadedFileInfo
    {
        public UploadedFileInfo(string id, string filename, Uri previewUri)
        {
            Id = id;
            Filename = filename;
            PreviewUri = previewUri;
        }

        public string Id { get; }
        public string Filename { get; }
        public Uri PreviewUri { get; }
    }
}
using System;

namespace NuClear.VStore.Descriptors.Sessions
{
    public sealed class BinaryMetadata
    {
        public BinaryMetadata(string filename, long filesize, Uri previewUri)
        {
            Filename = filename;
            Filesize = filesize;
            PreviewUri = previewUri;
        }

        public string Filename { get; }
        public long Filesize { get; }
        public Uri PreviewUri { get; }
    }
}
namespace NuClear.VStore.Descriptors.Sessions
{
    public sealed class BinaryMetadata
    {
        public BinaryMetadata(string filename, long filesize)
        {
            Filename = filename;
            Filesize = filesize;
        }

        public string Filename { get; }
        public long Filesize { get; }
    }
}
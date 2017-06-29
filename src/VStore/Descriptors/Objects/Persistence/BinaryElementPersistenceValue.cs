namespace NuClear.VStore.Descriptors.Objects.Persistence
{
    public sealed class BinaryElementPersistenceValue : IBinaryElementPersistenceValue
    {
        public BinaryElementPersistenceValue(string raw, string filename, long filesize)
        {
            Raw = raw;
            Filename = filename;
            Filesize = filesize;
        }

        public string Raw { get; }
        public string Filename { get; }
        public long Filesize { get; }
    }
}
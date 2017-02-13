namespace NuClear.VStore.Descriptors.Objects
{
    public sealed class ArticleElementValue : IBinaryElementValue
    {
        public string Raw { get; set; }
        public string Filename { get; set; }
        public long Filesize { get; set; }
    }
}
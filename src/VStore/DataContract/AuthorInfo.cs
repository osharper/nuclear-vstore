namespace NuClear.VStore.DataContract
{
    public sealed class AuthorInfo
    {
        public AuthorInfo(string author, string authorLogin, string authorName)
        {
            Author = author;
            AuthorLogin = authorLogin;
            AuthorName = authorName;
        }

        public string Author { get; }
        public string AuthorLogin { get; }
        public string AuthorName { get; }
    }
}
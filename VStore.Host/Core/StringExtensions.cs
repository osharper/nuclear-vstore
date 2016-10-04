namespace NuClear.VStore.Host.Core
{
    public static class StringExtensions
    {
        public static string AsMetadata(this string value) => $"x-amz-meta-{value}";
    }
}
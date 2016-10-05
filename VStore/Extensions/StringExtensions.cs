namespace NuClear.VStore.Extensions
{
    public static class StringExtensions
    {
        public static string AsMetadata(this string value) => $"x-amz-meta-{value}";
    }
}
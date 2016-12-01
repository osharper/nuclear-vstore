using System;
using System.Linq;

namespace NuClear.VStore.S3
{
    public static class KeyTypesExtensions
    {
        private const string Separator = "/";

        public static string AsS3ObjectKey(this Guid id, params object[] components)
        {
            return string.Join(Separator, new[] { id.ToString() }.Concat(components));
        }

        public static string AsS3ObjectKey(this long id, params object[] components)
        {
            return string.Join(Separator, new[] { id.ToString() }.Concat(components));
        }

        public static long AsObjectId(this string key)
        {
            var separatorIndex = key.LastIndexOf(Separator, StringComparison.Ordinal);
            return long.Parse(key.Substring(separatorIndex + 1));
        }
    }
}
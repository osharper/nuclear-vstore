using System;
using System.Linq;

namespace NuClear.VStore.S3
{
    public static class KeyTypesExtensions
    {
        private const string Separator = "/";
        private const string LockFinaliter = "#";

        public static string AsS3ObjectKey(this Guid id, params object[] components)
        {
            return string.Join(Separator, new[] { id.ToString() }.Concat(components));
        }

        public static string AsS3ObjectKey(this long id, params object[] components)
        {
            return string.Join(Separator, new[] { id.ToString() }.Concat(components));
        }

        public static long AsRootObjectId(this string key)
        {
            var separatorIndex = key.IndexOf(Separator, StringComparison.Ordinal);
            return long.Parse(key.Substring(0, separatorIndex));
        }

        public static long AsSubObjectId(this string key)
        {
            var separatorIndex = key.IndexOf(Separator, StringComparison.Ordinal);
            return long.Parse(key.Substring(separatorIndex + 1));
        }

        public static string AsS3LockKey(this long id)
        {
            return string.Concat(id.ToString(), LockFinaliter);
        }

        public static long AsLockObjectId(this string key)
        {
            var separatorIndex = key.IndexOf(LockFinaliter, StringComparison.Ordinal);
            return long.Parse(key.Substring(0, separatorIndex));
        }
    }
}

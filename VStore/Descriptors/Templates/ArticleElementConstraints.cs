using System;
using System.Collections.Generic;
using System.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class ArticleElementConstraints : IBinaryElementConstraints, IEquatable<ArticleElementConstraints>
    {
        public int? MaxSize { get; set; }
        public int? MaxFilenameLength { get; set; }
        public IEnumerable<FileFormat> SupportedFileFormats { get; set; }

        public bool Equals(ArticleElementConstraints other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (SupportedFileFormats == null && other.SupportedFileFormats != null ||
                SupportedFileFormats != null && other.SupportedFileFormats == null)
            {
                return false;
            }

            return MaxSize == other.MaxSize &&
                   MaxFilenameLength == other.MaxFilenameLength &&
                   (ReferenceEquals(SupportedFileFormats, other.SupportedFileFormats) || SupportedFileFormats.SequenceEqual(other.SupportedFileFormats));
        }

        public override bool Equals(object obj)
        {
            var other = obj as ArticleElementConstraints;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = MaxSize.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxFilenameLength.GetHashCode();
                hashCode = (hashCode * 397) ^ (SupportedFileFormats?.GetHashCode() ?? 0);
                return hashCode;
            }
        }
    }
}
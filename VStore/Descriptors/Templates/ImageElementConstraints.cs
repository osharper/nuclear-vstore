using System;
using System.Collections.Generic;
using System.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class ImageElementConstraints : IBinaryElementConstraints, IEquatable<ImageElementConstraints>
    {
        public int? MaxSize { get; set; }
        public int? MaxFilenameLength { get; set; }
        public IEnumerable<FileFormat> SupportedFileFormats { get; set; }
        public IEnumerable<ImageSize> SupportedImageSizes { get; set; }
        public bool IsAlphaChannelRequired { get; set; }

        public bool Equals(ImageElementConstraints other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            return MaxSize == other.MaxSize &&
                   MaxFilenameLength == other.MaxFilenameLength &&
                   SupportedFileFormats.SequenceEqual(other.SupportedFileFormats) &&
                   SupportedImageSizes.SequenceEqual(other.SupportedImageSizes) &&
                   IsAlphaChannelRequired == other.IsAlphaChannelRequired;
        }

        public override bool Equals(object obj)
        {
            var other = obj as ImageElementConstraints;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = MaxSize.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxFilenameLength.GetHashCode();
                hashCode = (hashCode * 397) ^ (SupportedFileFormats?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (SupportedImageSizes?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ IsAlphaChannelRequired.GetHashCode();
                return hashCode;
            }
        }
    }
}
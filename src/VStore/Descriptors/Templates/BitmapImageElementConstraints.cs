using System;
using System.Collections.Generic;
using System.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class BitmapImageElementConstraints : IBinaryElementConstraints, IImageElementConstraints, IEquatable<BitmapImageElementConstraints>
    {
        public int? MaxSize { get; set; }
        public int? MaxFilenameLength { get; set; }
        public IEnumerable<FileFormat> SupportedFileFormats { get; set; }
        public IEnumerable<ImageSize> SupportedImageSizes { get; set; }
        public bool IsAlphaChannelRequired { get; set; }
        public bool BinaryExists => true;
        public bool ValidImage => true;

        public bool ExtensionMatchContentFormat => true;

        public bool Equals(BitmapImageElementConstraints other)
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

            if (SupportedImageSizes == null && other.SupportedImageSizes != null ||
                SupportedImageSizes != null && other.SupportedImageSizes == null)
            {
                return false;
            }

            return MaxSize == other.MaxSize &&
                   MaxFilenameLength == other.MaxFilenameLength &&
                   BinaryExists == other.BinaryExists &&
                   ValidImage == other.ValidImage &&
                   ExtensionMatchContentFormat == other.ExtensionMatchContentFormat &&
                   (ReferenceEquals(SupportedFileFormats, other.SupportedFileFormats) || SupportedFileFormats.SequenceEqual(other.SupportedFileFormats)) &&
                   (ReferenceEquals(SupportedImageSizes, other.SupportedImageSizes) || SupportedImageSizes.SequenceEqual(other.SupportedImageSizes)) &&
                   IsAlphaChannelRequired == other.IsAlphaChannelRequired;
        }

        public override bool Equals(object obj)
        {
            var other = obj as BitmapImageElementConstraints;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = MaxSize.GetHashCode();
                hashCode = (hashCode * 397) ^ BinaryExists.GetHashCode();
                hashCode = (hashCode * 397) ^ ValidImage.GetHashCode();
                hashCode = (hashCode * 397) ^ ExtensionMatchContentFormat.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxFilenameLength.GetHashCode();
                hashCode = (hashCode * 397) ^ (SupportedFileFormats?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ (SupportedImageSizes?.GetHashCode() ?? 0);
                hashCode = (hashCode * 397) ^ IsAlphaChannelRequired.GetHashCode();
                return hashCode;
            }
        }
    }
}

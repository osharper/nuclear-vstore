using System;
using System.Collections.Generic;
using System.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public sealed class LogoElementConstraints : IBinaryElementConstraints, IImageElementConstraints, IEquatable<LogoElementConstraints>
    {
        public ImageSizeRange ImageSizeRange { get; set; }
        public bool AlphaChannelNotAllowed => true;
        public CropShape CropShape { get; set; }

        public bool CustomImageAlphaChannelRequired => true;
        public int? CustomImageMaxSize { get; set; }
        public bool CropAreaConsistentWithCropShape => true;
        public bool CropAreaConsistentWithImageSizeRange => true;
        public bool CustomImageConsistentWithCropShape => true;
        public ImageSizeRange CustomImageSizeRange { get; set; }
        public IEnumerable<FileFormat> CustomImageSupportedFileFormats { get; set; }

        public IEnumerable<FileFormat> SupportedFileFormats { get; set; }
        public int? MaxSize { get; set; }
        public int? MaxFilenameLength { get; set; }

        public bool BinaryExists => true;
        public bool ValidImage => true;

        #region Equality members

        public bool Equals(LogoElementConstraints other)
        {
            if (ReferenceEquals(null, other))
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return ImageSizeRange.Equals(other.ImageSizeRange) && CropShape == other.CropShape && CustomImageMaxSize == other.CustomImageMaxSize &&
                   CustomImageSizeRange.Equals(other.CustomImageSizeRange) && MaxSize == other.MaxSize && MaxFilenameLength == other.MaxFilenameLength &&
                   (ReferenceEquals(SupportedFileFormats, other.SupportedFileFormats) || SupportedFileFormats.SequenceEqual(other.SupportedFileFormats)) &&
                   (ReferenceEquals(CustomImageSupportedFileFormats, other.CustomImageSupportedFileFormats) ||
                    CustomImageSupportedFileFormats.SequenceEqual(other.CustomImageSupportedFileFormats));
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            if (ReferenceEquals(this, obj))
                return true;
            return obj is LogoElementConstraints && Equals((LogoElementConstraints)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = ImageSizeRange.GetHashCode();
                hashCode = (hashCode * 397) ^ (int)CropShape;
                hashCode = (hashCode * 397) ^ CustomImageMaxSize.GetHashCode();
                hashCode = (hashCode * 397) ^ CustomImageSizeRange.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxSize.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxFilenameLength.GetHashCode();
                return hashCode;
            }
        }

        #endregion
    }
}
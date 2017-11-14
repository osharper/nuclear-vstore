using System;
using System.Collections.Generic;
using System.Linq;

namespace NuClear.VStore.Descriptors.Templates
{
    public class VectorImageElementConstraints : IBinaryElementConstraints, IImageElementConstraints, IEquatable<VectorImageElementConstraints>
    {
        public int? MaxSize { get; set; }
        public int? MaxFilenameLength { get; set; }
        public IEnumerable<FileFormat> SupportedFileFormats { get; set; }
        public bool BinaryExists => true;
        public bool ValidImage => true;
        public bool WithoutBitmaps => true;
        public bool WithoutNonRenderedElements => true;
        public bool WithoutUrlInStyles => true;
        public bool WithoutGradient => true;
        public bool PathsAreClosed => true;

        public bool Equals(VectorImageElementConstraints other)
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
                   (ReferenceEquals(SupportedFileFormats, other.SupportedFileFormats) || SupportedFileFormats.SequenceEqual(other.SupportedFileFormats)) &&
                   BinaryExists == other.BinaryExists &&
                   ValidImage == other.ValidImage &&
                   WithoutBitmaps == other.WithoutBitmaps &&
                   WithoutNonRenderedElements == other.WithoutNonRenderedElements &&
                   WithoutUrlInStyles == other.WithoutUrlInStyles &&
                   WithoutGradient == other.WithoutGradient &&
                   PathsAreClosed == other.PathsAreClosed;
        }

        public override bool Equals(object obj)
        {
            var other = obj as VectorImageElementConstraints;
            return Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = MaxSize.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxFilenameLength.GetHashCode();
                hashCode = (hashCode * 397) ^ BinaryExists.GetHashCode();
                hashCode = (hashCode * 397) ^ ValidImage.GetHashCode();
                hashCode = (hashCode * 397) ^ WithoutBitmaps.GetHashCode();
                hashCode = (hashCode * 397) ^ WithoutNonRenderedElements.GetHashCode();
                hashCode = (hashCode * 397) ^ WithoutUrlInStyles.GetHashCode();
                hashCode = (hashCode * 397) ^ WithoutGradient.GetHashCode();
                hashCode = (hashCode * 397) ^ PathsAreClosed.GetHashCode();
                hashCode = (hashCode * 397) ^ (SupportedFileFormats != null ? SupportedFileFormats.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}

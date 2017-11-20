using System;

namespace NuClear.VStore.Descriptors
{
    public struct ImageSizeRange : IEquatable<ImageSizeRange>
    {
        public ImageSize Min { get; set; }
        public ImageSize Max { get; set; }

        public bool Includes(ImageSize size)
        {
            return Min.Width <= size.Width && Min.Height <= size.Height && Max.Width >= size.Width && Max.Height >= size.Height;
        }

        public bool Equals(ImageSizeRange other)
        {
            return Min.Equals(other.Min) && Max.Equals(other.Max);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
                return false;
            return obj is ImageSizeRange range && Equals(range);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Min.GetHashCode() * 397) ^ Max.GetHashCode();
            }
        }
    }
}
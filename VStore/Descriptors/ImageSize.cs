namespace NuClear.VStore.Descriptors
{
    public struct ImageSize
    {
        public static ImageSize Empty { get; } = new ImageSize();

        public int Width { get; set; }

        public int Height { get; set; }

        public static bool operator ==(ImageSize obj1, ImageSize obj2)
        {
            return obj1.Height == obj2.Height && obj1.Width == obj2.Width;
        }

        public static bool operator !=(ImageSize obj1, ImageSize obj2)
        {
            return !(obj1 == obj2);
        }

        public override bool Equals(object obj)
        {
            return obj is ImageSize && this == (ImageSize)obj;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Height * 397) ^ Width;
            }
        }

        public override string ToString()
        {
            return $"{Width}x{Height}";
        }
    }
}
namespace NuClear.VStore.Descriptors
{
    public struct ImageSize
    {
        public ImageSize(int width, int legnth)
        {
            Width = width;
            Legnth = legnth;
        }

        public int Width { get; }
        public int Legnth { get; }
    }
}
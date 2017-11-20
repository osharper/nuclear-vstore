using System.Collections.Generic;

using NuClear.VStore.Descriptors;

using SixLabors.ImageSharp;

namespace NuClear.VStore.Sessions.ContentValidation
{
    public static class ImageUtils
    {
        public static IReadOnlyDictionary<FileFormat, string> ImageFormat2MimeTypeMap { get; } =
            new Dictionary<FileFormat, string>
                {
                    { FileFormat.Bmp, "image/bmp" },
                    { FileFormat.Gif, "image/gif" },
                    { FileFormat.Jpeg, "image/jpeg" },
                    { FileFormat.Jpg, "image/jpeg" },
                    { FileFormat.Png, "image/png" }
                };

        public static bool IsImageContainsAlphaChannel(Image<Rgba32> image)
        {
            for (var x = 0; x < image.Width; ++x)
            {
                for (var y = 0; y < image.Height; ++y)
                {
                    if (image[x, y].A != byte.MaxValue)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
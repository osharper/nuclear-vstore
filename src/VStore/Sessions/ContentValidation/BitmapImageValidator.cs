using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Sessions.ContentValidation.Errors;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace NuClear.VStore.Sessions.ContentValidation
{
    public static class BitmapImageValidator
    {
        private static readonly IReadOnlyDictionary<FileFormat, string> BitmapImageFormatsMap =
            new Dictionary<FileFormat, string>
                {
                    { FileFormat.Bmp, "image/bmp" },
                    { FileFormat.Gif, "image/gif" },
                    { FileFormat.Jpeg, "image/jpeg" },
                    { FileFormat.Jpg, "image/jpeg" },
                    { FileFormat.Png, "image/png" }
                };

        public static void ValidateBitmapImageHeader(int templateCode, FileFormat fileFormat, Stream inputStream)
        {
            var format = Image.DetectFormat(inputStream);
            if (format == null)
            {
                throw new InvalidBinaryException(templateCode, new InvalidImageError());
            }

            var extension = fileFormat.ToString().ToLowerInvariant();
            // Image format is not consistent with filename extension:
            if (!format.FileExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidBinaryException(templateCode, new BinaryExtensionMismatchContentError(extension, format.Name.ToLowerInvariant()));
            }
        }

        public static string ValidateBitmapImage(int templateCode, BitmapImageElementConstraints constraints, Stream inputStream)
        {
            var imageFormats = constraints.SupportedFileFormats
                                          .Aggregate(
                                              new List<string>(),
                                              (result, next) =>
                                              {
                                                  if (BitmapImageFormatsMap.TryGetValue(next, out var imageFormat))
                                                  {
                                                      result.Add(imageFormat);
                                                  }

                                                  return result;
                                              });

            Image<Rgba32> image;
            IImageFormat format;
            try
            {
                image = Image.Load(inputStream, out format);
            }
            catch (Exception)
            {
                throw new InvalidBinaryException(templateCode, new InvalidImageError());
            }

            using (image)
            {
                if (!imageFormats.Contains(format.DefaultMimeType, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidBinaryException(templateCode, new BinaryInvalidFormatError(format.Name.ToLowerInvariant()));
                }

                if (constraints.SupportedImageSizes.All(x => image.Width != x.Width || image.Height != x.Height))
                {
                    throw new InvalidBinaryException(templateCode, new ImageUnsupportedSizeError(new ImageSize { Height = image.Height, Width = image.Width }));
                }

                if (constraints.IsAlphaChannelRequired && !IsImageContainsAlphaChannel(image))
                {
                    throw new InvalidBinaryException(templateCode, new ImageAlphaChannelError());
                }
            }

            return format.DefaultMimeType;
        }

        private static bool IsImageContainsAlphaChannel(Image<Rgba32> image)
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

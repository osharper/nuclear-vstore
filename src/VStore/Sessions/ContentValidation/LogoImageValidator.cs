using System;
using System.IO;
using System.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Sessions.ContentValidation.Errors;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace NuClear.VStore.Sessions.ContentValidation
{
    public static class LogoImageValidator
    {
        public static string ValidateLogo(int templateCode, LogoElementConstraints constraints, Stream inputStream)
        {
            var supportedMimeTypes = constraints.SupportedFileFormats
                                                .Select(x => ImageUtils.ImageFormat2MimeTypeMap[x])
                                                .ToList();

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
                if (!supportedMimeTypes.Contains(format.DefaultMimeType, StringComparer.OrdinalIgnoreCase))
                {
                    throw new InvalidBinaryException(templateCode, new BinaryInvalidFormatError(format.Name.ToLowerInvariant()));
                }

                if (image.Width < constraints.ImageSizeRange.Min.Width || image.Height < constraints.ImageSizeRange.Min.Height ||
                    image.Width > constraints.ImageSizeRange.Max.Width || image.Height > constraints.ImageSizeRange.Max.Height)
                {
                    throw new InvalidBinaryException(templateCode, new ImageSizeOutOfRangeError(new ImageSize { Height = image.Height, Width = image.Width }));
                }

                if (ImageUtils.IsImageContainsAlphaChannel(image))
                {
                    throw new InvalidBinaryException(templateCode, new ImageHasAlphaChannelError());
                }
            }

            return format.DefaultMimeType;
        }

        public static void ValidateLogoImageHeader(int templateCode, FileFormat fileFormat, Stream inputStream)
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
    }
}
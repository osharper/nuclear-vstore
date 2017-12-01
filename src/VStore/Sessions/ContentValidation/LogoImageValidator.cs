using System;
using System.IO;
using System.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Objects.ContentValidation.Errors;
using NuClear.VStore.Sessions.ContentValidation.Errors;
using NuClear.VStore.Sessions.UploadParams;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;

namespace NuClear.VStore.Sessions.ContentValidation
{
    public static class LogoImageValidator
    {
        public static string ValidateLogoOriginal(int templateCode, LogoElementConstraints constraints, Stream inputStream)
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

                var imageSize = new ImageSize { Width = image.Width, Height = image.Height };
                if (!constraints.ImageSizeRange.Includes(imageSize))
                {
                    throw new InvalidBinaryException(templateCode, new ImageSizeOutOfRangeError(imageSize));
                }

                // TODO: раскомментить/удалить после согласования проверок на альфа канал
                //if (ImageUtils.IsImageContainsAlphaChannel(image))
                //{
                //    throw new InvalidBinaryException(templateCode, new ImageHasAlphaChannelError());
                //}
            }

            return format.DefaultMimeType;
        }

        public static string ValidateLogoCustomImage(int templateCode, LogoElementConstraints constraints, Stream inputStream, CustomImageFileUploadParams customImageFileUploadParams)
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

                var imageSize = new ImageSize { Width = image.Width, Height = image.Height };
                if (imageSize != customImageFileUploadParams.Size)
                {
                    throw new InvalidBinaryException(templateCode, new CustomImageTargetSizeNotEqualToActualSize(imageSize));
                }

                if (constraints.CustomImageIsSquare && imageSize.Width != imageSize.Height)
                {
                    throw new InvalidBinaryException(templateCode, new CustomImageIsNotSquareError());
                }

                if (!constraints.CustomImageSizeRange.Includes(imageSize))
                {
                    throw new InvalidBinaryException(templateCode, new CustomImageSizeOutOfRangeError(imageSize));
                }
            }

            return format.DefaultMimeType;
        }
    }
}
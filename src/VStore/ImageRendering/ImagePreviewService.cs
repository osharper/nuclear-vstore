using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Objects;
using NuClear.VStore.Options;
using NuClear.VStore.S3;
using NuClear.VStore.Sessions;
using NuClear.VStore.Sessions.ContentValidation.Errors;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using SixLabors.Shapes;

namespace NuClear.VStore.ImageRendering
{
    public sealed class ImagePreviewService
    {
        private static readonly Dictionary<string, IImageEncoder> Encoders =
            new Dictionary<string, IImageEncoder>
                {
                    { ImageFormats.Jpeg.DefaultMimeType, new JpegEncoder { Quality = 100, IgnoreMetadata = true } },
                    { ImageFormats.Png.DefaultMimeType, new PngEncoder { CompressionLevel = 1, IgnoreMetadata = true } },
                    { ImageFormats.Gif.DefaultMimeType, new GifEncoder { IgnoreMetadata = true } }
                };

        private readonly string _bucketName;
        private readonly IS3Client _s3Client;
        private readonly ObjectsStorageReader _objectsStorageReader;

        public ImagePreviewService(CephOptions cephOptions, IS3Client s3Client, ObjectsStorageReader objectsStorageReader)
        {
            _bucketName = cephOptions.FilesBucketName;
            _s3Client = s3Client;
            _objectsStorageReader = objectsStorageReader;
        }

        public async Task<(Stream imageStream, string contentType)> GetPreview(
            long id,
            string versionId,
            int templateCode,
            int width,
            int height)
        {
            var rawStream = await GetRawStream(id, versionId, templateCode);
            using (var image = LoadImage(templateCode, rawStream, out var format))
            {
                Resize(image, new Size(width, height));

                var imageStream = Save(image, format);
                return (imageStream, format.DefaultMimeType);
            }
        }

        [Obsolete]
        public async Task<(Stream imageStream, string contentType)> GetRoundedPreview(
            long id,
            string versionId,
            int templateCode,
            int width,
            int height)
        {
            var rawStream = await GetRawStream(id, versionId, templateCode);
            using (var image = LoadImage(templateCode, rawStream, out var format))
            {
                ApplyRoundedCorners(image, image.Height * 0.5f);
                Resize(image, new Size(width, height));

                var imageStream = Save(image, format);
                return (imageStream, format.DefaultMimeType);
            }
        }

        private static Image<Rgba32> LoadImage(int templateCode, Stream sourceStream, out IImageFormat format)
        {
            using (sourceStream)
            {
                Image<Rgba32> image;
                try
                {
                    image = Image.Load(sourceStream, out format);
                }
                catch (Exception)
                {
                    throw new InvalidBinaryException(templateCode, new InvalidImageError());
                }

                return image;
            }
        }

        private static void Resize(Image<Rgba32> image, Size size)
        {
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = size,
                    Sampler = new Lanczos2Resampler(),
                    Mode = ResizeMode.Crop
                }));
        }

        private MemoryStream Save(Image<Rgba32> image, IImageFormat format)
        {
            var imageStream = new MemoryStream();
            image.Save(imageStream, Encoders[format.DefaultMimeType]);
            imageStream.Position = 0;

            return imageStream;
        }

        private static void ApplyRoundedCorners(Image<Rgba32> image, float cornerRadius)
        {
            var corners = GetClippedRect(image.Width, image.Height, cornerRadius);
            image.Mutate(x => x.Fill(Rgba32.Transparent,
                                     corners,
                                     new GraphicsOptions(true) { BlenderMode = PixelBlenderMode.Src }));
        }

        private static IPath GetClippedRect(int imageWidth, int imageHeight, float cornerRadius)
        {
            var rect = new RectangularePolygon(-0.5f, -0.5f, imageWidth + 0.5f, imageHeight + 0.5f);
            return rect.Clip(new EllipsePolygon(imageWidth * 0.5f, imageHeight * 0.5f, cornerRadius));
        }

        private async Task<Stream> GetRawStream(long id, string versionId, int templateCode)
        {
            var objectDescriptor = await _objectsStorageReader.GetObjectDescriptor(id, versionId);
            var element = objectDescriptor.Elements.SingleOrDefault(x => x.TemplateCode == templateCode);
            if (element == null)
            {
                throw new ObjectNotFoundException($"Element with template code '{templateCode}' of object/versionId '{id}/{versionId}' not found.");
            }

            if (!(element.Value is IImageElementValue elementValue))
            {
                throw new InvalidOperationException($"Element with template code '{templateCode}' of object/versionId '{id}/{versionId}' is not an image.");
            }

            var response = await _s3Client.GetObjectAsync(_bucketName, elementValue.Raw);

            return response.ResponseStream;
        }
    }
}
using System.IO;
using System.Net.Http;

using Microsoft.AspNetCore.Mvc;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using SixLabors.Shapes;

namespace NuClear.VStore.Renderer.Controllers
{
    [Route("preview")]
    public class PreviewController : ControllerBase
    {
        private static readonly Stream SourceStream;
        private static readonly object Sync = new object();

        static PreviewController()
        {
            var httpClient = new HttpClient();
            var stream = httpClient.GetStreamAsync("https://ul.haircontrast.com/Haircontrast-logo-128px-small-nordic-hair-contrast.jpg").GetAwaiter().GetResult();

            SourceStream = new MemoryStream();
            stream.CopyTo(SourceStream);
            SourceStream.Position = 0;
        }

        [HttpGet]
        public IActionResult Get(int size)
        {
            Image<Rgba32> source;
            lock (Sync)
            {
                source = Image.Load(SourceStream);
                SourceStream.Position = 0;
            }
            try
            {
                ConvertToCircle(source, source.Height*0.5f, new Size(size, size));

                var resultStream = new MemoryStream();
                source.Save(resultStream, ImageFormats.Png);

                resultStream.Position = 0;
                return new FileStreamResult(resultStream, "image/png");
            }
            finally
            {
                source?.Dispose();
            }
        }

        [HttpGet("local")]
        public IActionResult GetLocal(int size)
        {
            var file = System.IO.File.Open($"/Volumes/USB/downloads/macos/image_{size}x{size}.png", FileMode.Open);
            return new FileStreamResult(file, "image/png");
        }

        private static void ConvertToCircle(Image<Rgba32> image, float cornerRadius, Size size)
        {
            ApplyRoundedCorners(image, cornerRadius);
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = size,
                    Sampler = new Lanczos2Resampler(),
                    Mode = ResizeMode.Crop
                }));
        }

        public static void ApplyRoundedCorners(Image<Rgba32> image, float cornerRadius)
        {
            var corners = GetClippedRect(image.Width, image.Height, cornerRadius);
            image.Mutate(x => x.Fill(Rgba32.Transparent,
                                     corners,
                                     new GraphicsOptions(true) { BlenderMode = PixelBlenderMode.Src }));
        }

        public static IPath GetClippedRect(int imageWidth, int imageHeight, float cornerRadius)
        {
            var rect = new RectangularePolygon(-0.5f, -0.5f, imageWidth + 0.5f, imageHeight + 0.5f);
            return rect.Clip(new EllipsePolygon(imageWidth * 0.5f, imageHeight * 0.5f, cornerRadius));
        }
    }
}
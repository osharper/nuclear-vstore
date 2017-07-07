using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Json;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class ImageUnsupportedSizeError : BinaryValidationError
    {
        public ImageUnsupportedSizeError(ImageSize imageSize)
        {
            ImageSize = imageSize;
        }

        public ImageSize ImageSize { get; }

        public override BinaryConstraintViolations ErrorType => BinaryConstraintViolations.SupportedImageSizes;

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = JToken.FromObject(ImageSize, JsonSerializer);
            return ret;
        }
    }
}

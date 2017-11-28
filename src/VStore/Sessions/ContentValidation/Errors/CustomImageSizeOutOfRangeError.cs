using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public sealed class CustomImageSizeOutOfRangeError : BinaryValidationError
    {
        public CustomImageSizeOutOfRangeError(ImageSize imageSize)
        {
            ImageSize = imageSize;
        }

        public ImageSize ImageSize { get; }

        public override string ErrorType => nameof(LogoElementConstraints.CustomImageSizeRange);

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = JToken.FromObject(ImageSize, JsonSerializer);
            return ret;
        }
    }
}
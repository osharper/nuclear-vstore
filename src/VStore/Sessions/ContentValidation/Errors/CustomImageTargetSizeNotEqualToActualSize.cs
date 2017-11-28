using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public sealed class CustomImageTargetSizeNotEqualToActualSize : BinaryValidationError
    {
        public CustomImageTargetSizeNotEqualToActualSize(ImageSize actualSize)
        {
            ActualSize = actualSize;
        }

        public ImageSize ActualSize { get; }

        public override string ErrorType => nameof(LogoElementConstraints.CustomImageTargetSizeEqualToActualSize);

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = JToken.FromObject(ActualSize, JsonSerializer);
            return ret;
        }
    }
}
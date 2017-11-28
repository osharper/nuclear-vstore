using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public sealed class CustomImageInvalidFormatError : BinaryValidationError
    {
        public CustomImageInvalidFormatError(string extension)
        {
            Extension = extension;
        }

        public string Extension { get; }

        public override string ErrorType => nameof(LogoElementConstraints.CustomImageSupportedFileFormats);

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = Extension;
            return ret;
        }
    }
}
using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class BinaryExtensionMismatchContentError : BinaryValidationError
    {
        public BinaryExtensionMismatchContentError(string extension, string contentFormat)
        {
            Extension = extension;
            ContentFormat = contentFormat;
        }

        public string Extension { get; }

        public string ContentFormat { get; }

        public override string ErrorType => nameof(BitmapImageElementConstraints.ExtensionMatchContentFormat);

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = JToken.FromObject(new { Extension, ContentFormat }, JsonSerializer);
            return ret;
        }
    }
}

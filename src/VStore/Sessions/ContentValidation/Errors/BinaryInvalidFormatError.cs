using Newtonsoft.Json.Linq;

using NuClear.VStore.Json;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class BinaryInvalidFormatError : BinaryValidationError
    {
        public BinaryInvalidFormatError(string extension)
        {
            Extension = extension;
        }

        public string Extension { get; }

        public override BinaryConstraintViolations ErrorType => BinaryConstraintViolations.SupportedFileFormats;

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = Extension;
            return ret;
        }
    }
}

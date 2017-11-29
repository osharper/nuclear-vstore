using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public sealed class CustomImageTooLargeError : BinaryValidationError
    {
        public CustomImageTooLargeError(long inputStreamLength)
        {
            InputStreamLength = inputStreamLength;
        }

        public long InputStreamLength { get; }

        public override string ErrorType => nameof(LogoElementConstraints.CustomImageMaxSize);

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = InputStreamLength;
            return ret;
        }
    }
}
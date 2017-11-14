using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class BinaryTooLargeError : BinaryValidationError
    {
        public BinaryTooLargeError(long inputStreamLength)
        {
            InputStreamLength = inputStreamLength;
        }

        public long InputStreamLength { get; }

        public override string ErrorType => nameof(IBinaryElementConstraints.MaxSize);

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = InputStreamLength;
            return ret;
        }
    }
}

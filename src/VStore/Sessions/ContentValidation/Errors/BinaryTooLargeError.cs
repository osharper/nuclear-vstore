using Newtonsoft.Json.Linq;

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

        public override BinaryConstraintViolations ErrorType => BinaryConstraintViolations.MaxSize;

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = InputStreamLength;
            return ret;
        }
    }
}

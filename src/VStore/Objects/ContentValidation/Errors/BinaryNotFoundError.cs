using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public sealed class BinaryNotFoundError : ObjectElementValidationError
    {
        public BinaryNotFoundError(string rawValue)
        {
            RawValue = rawValue;
        }

        public string RawValue { get; }

        public override string ErrorType => nameof(IBinaryElementConstraints.BinaryExists);

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = RawValue;
            return ret;
        }
    }
}

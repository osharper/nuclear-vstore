using Newtonsoft.Json.Linq;

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

        public override ElementValidationErrors ErrorType => ElementValidationErrors.BinaryNotFound;

        public override JToken SerializeToJson()
        {
            return new JObject
                {
                    [Tokens.TypeToken] = "binaryNotFound",
                    [Tokens.ValueToken] = RawValue
                };
        }
    }
}
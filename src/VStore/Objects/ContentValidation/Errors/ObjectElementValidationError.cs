using Newtonsoft.Json.Linq;

using NuClear.VStore.Json;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public abstract class ObjectElementValidationError
    {
        public abstract ElementConstraintViolations ErrorType { get; }

        public virtual JToken SerializeToJson()
        {
            var error = ErrorType.ToString();
            return new JObject
            {
                [Tokens.TypeToken] = char.ToLower(error[0]).ToString() + error.Substring(1),
                [Tokens.ValueToken] = true
            };
        }
    }
}

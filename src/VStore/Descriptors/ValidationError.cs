using Newtonsoft.Json.Linq;

using NuClear.VStore.Json;

namespace NuClear.VStore.Descriptors
{
    public abstract class ValidationError
    {
        public abstract string ErrorType { get; }

        public virtual JToken SerializeToJson()
        {
            var error = ErrorType;
            return new JObject
                {
                    [Tokens.TypeToken] = char.ToLower(error[0]).ToString() + error.Substring(1),
                    [Tokens.ValueToken] = true
                };
        }
    }
}

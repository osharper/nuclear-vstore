using Newtonsoft.Json.Linq;

using NuClear.VStore.Json;

namespace NuClear.VStore.Descriptors
{
    public abstract class ValidationError<TViolation>
    {
        public abstract TViolation ErrorType { get; }

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

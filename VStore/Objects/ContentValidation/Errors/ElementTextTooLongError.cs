using Newtonsoft.Json.Linq;

using NuClear.VStore.Json;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class ElementTextTooLongError : ObjectElementValidationError
    {
        public ElementTextTooLongError(int maxLength, int actualLength)
        {
            MaxLength = maxLength;
            ActualLength = actualLength;
        }

        public int MaxLength { get; }

        public int ActualLength { get; }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.TextTooLong;

        public override JToken SerializeToJson()
        {
            return new JObject
            {
                [Tokens.TypeToken] = "maxSymbols",
                [Tokens.ValueToken] = ActualLength
            };
        }
    }
}

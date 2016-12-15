using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Json;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class ElementWordsTooLongError : ObjectElementValidationError
    {
        public ElementWordsTooLongError(int maxWordLength, IReadOnlyCollection<string> tooLongWords)
        {
            MaxWordLength = maxWordLength;
            TooLongWords = tooLongWords;
        }

        public int MaxWordLength { get; }

        public IReadOnlyCollection<string> TooLongWords { get; }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.WordsTooLong;

        public override JToken SerializeToJson()
        {
            return new JObject
            {
                [Tokens.TypeToken] = "maxSymbolsPerWord",
                [Tokens.ValueToken] = new JArray(TooLongWords)
            };
        }
    }
}

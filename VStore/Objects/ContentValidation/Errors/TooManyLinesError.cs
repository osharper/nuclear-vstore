using Newtonsoft.Json.Linq;

using NuClear.VStore.Json;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class TooManyLinesError : ObjectElementValidationError
    {
        public TooManyLinesError(int maxLinesCount, int actualLinesCount)
        {
            MaxLinesCount = maxLinesCount;
            ActualLinesCount = actualLinesCount;
        }

        public int MaxLinesCount { get; }

        public int ActualLinesCount { get; }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.TooManyLines;

        public override JToken SerializeToJson()
        {
            return new JObject
            {
                [Tokens.TypeToken] = "maxLines",
                [Tokens.ValueToken] = ActualLinesCount
            };
        }
    }
}

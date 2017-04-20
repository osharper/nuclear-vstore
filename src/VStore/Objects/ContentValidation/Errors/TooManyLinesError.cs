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

        public override ElementConstraintViolations ErrorType => ElementConstraintViolations.MaxLines;

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = ActualLinesCount;
            return ret;
        }
    }
}

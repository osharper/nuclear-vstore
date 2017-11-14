using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;
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

        public override string ErrorType => nameof(TextElementConstraints.MaxSymbolsPerWord);

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = new JArray(TooLongWords);
            return ret;
        }
    }
}

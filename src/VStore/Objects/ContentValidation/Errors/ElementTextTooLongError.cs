using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;
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

        public override string ErrorType => nameof(ITextElementConstraints.MaxSymbols);

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = ActualLength;
            return ret;
        }
    }
}

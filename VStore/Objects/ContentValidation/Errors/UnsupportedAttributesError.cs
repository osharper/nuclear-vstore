using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Json;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class UnsupportedAttributesError : ObjectElementValidationError
    {
        public UnsupportedAttributesError(IReadOnlyCollection<string> attributes)
        {
            UnsupportedAttributes = attributes;
        }

        public IReadOnlyCollection<string> UnsupportedAttributes { get; }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.UnsupportedAttributes;

        public override JToken SerializeToJson()
        {
            return new JObject
            {
                [Tokens.TypeToken] = "unsupportedAttributes",
                [Tokens.ValueToken] = new JArray(UnsupportedAttributes)
            };
        }
    }
}

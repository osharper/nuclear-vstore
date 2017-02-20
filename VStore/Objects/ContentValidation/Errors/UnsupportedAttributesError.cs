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

        public override ElementConstraintViolations ErrorType => ElementConstraintViolations.SupportedAttributes;

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = new JArray(UnsupportedAttributes);
            return ret;
        }
    }
}

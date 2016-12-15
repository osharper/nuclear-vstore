using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Json;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class UnsupportedTagsError : ObjectElementValidationError
    {
        public UnsupportedTagsError(IReadOnlyCollection<string> supportedTags, IReadOnlyCollection<string> unsupportedTags)
        {
            SupportedTags = supportedTags;
            UnsupportedTags = unsupportedTags;
        }

        public IReadOnlyCollection<string> SupportedTags { get; }

        public IReadOnlyCollection<string> UnsupportedTags { get; }

        public override ElementValidationErrors ErrorType => ElementValidationErrors.UnsupportedTags;

        public override JToken SerializeToJson()
        {
            return new JObject
            {
                [Tokens.TypeToken] = "unsupportedTags",
                [Tokens.ValueToken] = new JArray(UnsupportedTags)
            };
        }
    }
}

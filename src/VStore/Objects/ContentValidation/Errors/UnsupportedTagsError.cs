using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;
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

        public override string ErrorType => nameof(FormattedTextElementConstraints.SupportedTags);

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = new JArray(UnsupportedTags);
            return ret;
        }
    }
}

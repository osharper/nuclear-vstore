using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class ImageWithNonRenderedElementsError : BinaryValidationError
    {
        public ImageWithNonRenderedElementsError(IReadOnlyCollection<string> nonRenderedElements)
        {
            NonRenderedElements = nonRenderedElements;
        }

        public IReadOnlyCollection<string> NonRenderedElements { get; }

        public override string ErrorType => nameof(VectorImageElementConstraints.WithoutNonRenderedElements);

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = new JArray(NonRenderedElements);
            return ret;
        }
    }
}

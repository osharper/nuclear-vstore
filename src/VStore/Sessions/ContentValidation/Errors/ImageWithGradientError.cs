using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class ImageWithGradientError : BinaryValidationError
    {
        public ImageWithGradientError(IReadOnlyCollection<string> gradientElements)
        {
            GradientElements = gradientElements;
        }

        public IReadOnlyCollection<string> GradientElements { get; }

        public override string ErrorType => nameof(VectorImageElementConstraints.WithoutGradient);

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = new JArray(GradientElements);
            return ret;
        }
    }
}

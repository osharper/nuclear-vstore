using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;
using NuClear.VStore.Json;

namespace NuClear.VStore.Sessions.ContentValidation.Errors
{
    public class FilenameTooLongError : BinaryValidationError
    {
        public FilenameTooLongError(int actualLength)
        {
            ActualLength = actualLength;
        }

        public int ActualLength { get; }

        public override string ErrorType => nameof(IBinaryElementConstraints.MaxFilenameLength);

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = ActualLength;
            return ret;
        }
    }
}

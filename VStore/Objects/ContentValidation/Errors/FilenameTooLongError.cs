using Newtonsoft.Json.Linq;

using NuClear.VStore.Json;

namespace NuClear.VStore.Objects.ContentValidation.Errors
{
    public class FilenameTooLongError : ObjectElementValidationError
    {
        public FilenameTooLongError(int maxFilenameLength, int actualLength)
        {
            MaxFilenameLength = maxFilenameLength;
            ActualLength = actualLength;
        }

        public int MaxFilenameLength { get; }

        public int ActualLength { get; }

        public override ElementConstraintViolations ErrorType => ElementConstraintViolations.MaxFilenameLength;

        public override JToken SerializeToJson()
        {
            var ret = base.SerializeToJson();
            ret[Tokens.ValueToken] = ActualLength;
            return ret;
        }
    }
}

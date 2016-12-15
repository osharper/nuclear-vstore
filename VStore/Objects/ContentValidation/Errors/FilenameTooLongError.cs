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

        public override ElementValidationErrors ErrorType => ElementValidationErrors.FilenameTooLong;

        public override JToken SerializeToJson()
        {
            return new JObject
            {
                [Tokens.TypeToken] = "maxFilenameLength",
                [Tokens.ValueToken] = ActualLength
            };
        }
    }
}

using System;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Json;
using NuClear.VStore.Sessions.ContentValidation.Errors;

namespace NuClear.VStore.Sessions
{
    public class InvalidBinaryException : Exception
    {
        public InvalidBinaryException(int templateCode, BinaryValidationError error)
        {
            TemplateCode = templateCode;
            Error = error;
        }

        public int TemplateCode { get; }

        public BinaryValidationError Error { get; }

        public JToken SerializeToJson()
        {
            return new JObject
                {
                    [Tokens.TemplateCodeToken] = TemplateCode,
                    [Tokens.ErrorsToken] = new JArray { Error.SerializeToJson() }
                };
        }
    }
}

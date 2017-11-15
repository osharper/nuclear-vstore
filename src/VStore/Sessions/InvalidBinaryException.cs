using System;
using System.Collections.Generic;

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
            Errors = new[] { error };
        }

        public InvalidBinaryException(int templateCode, IReadOnlyCollection<BinaryValidationError> errors)
        {
            TemplateCode = templateCode;
            Errors = errors;
        }

        public int TemplateCode { get; }

        public IReadOnlyCollection<BinaryValidationError> Errors { get; }

        public JToken SerializeToJson()
        {
            var elementErrors = new JArray();
            foreach (var error in Errors)
            {
                elementErrors.Add(error.SerializeToJson());
            }

            return new JObject
                {
                    [Tokens.TemplateCodeToken] = TemplateCode,
                    [Tokens.ErrorsToken] = elementErrors
                };
        }
    }
}

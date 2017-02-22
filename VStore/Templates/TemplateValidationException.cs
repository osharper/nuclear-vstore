using System;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Json;

namespace NuClear.VStore.Templates
{
    public sealed class TemplateValidationException : Exception
    {
        public TemplateValidationException(int templateCode, TemplateElementValidationErrors error)
        {
            TemplateCode = templateCode;
            Error = error;
        }

        public int TemplateCode { get; }

        public TemplateElementValidationErrors Error { get; }

        public JToken SerializeToJson()
        {
            var error = Error.ToString();
            return new JObject
                       {
                           [Tokens.TemplateCodeToken] = TemplateCode,
                           [Tokens.ErrorToken] = char.ToLower(error[0]).ToString() + error.Substring(1)
                       };
        }
    }
}

using System;
using System.Collections.Generic;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Json;
using NuClear.VStore.Objects.ContentValidation.Errors;

namespace NuClear.VStore.Objects.ContentValidation
{
    public sealed class InvalidObjectException : Exception
    {
        public InvalidObjectException(long objectId, IDictionary<int, IReadOnlyCollection<ObjectElementValidationError>> elementErrors)
        {
            ObjectId = objectId;
            ElementErrors = elementErrors;
        }

        public long ObjectId { get; }

        public IDictionary<int, IReadOnlyCollection<ObjectElementValidationError>> ElementErrors { get; }

        public JToken SerializeToJson()
        {
            var elements = new JArray();
            foreach (var templateCode in ElementErrors.Keys)
            {
                var elementErrors = new JArray();
                foreach (var error in ElementErrors[templateCode])
                {
                    elementErrors.Add(error.SerializeToJson());
                }

                elements.Add(new JObject
                    {
                        { Tokens.TemplateCodeToken, templateCode },
                        { Tokens.ErrorsToken, elementErrors }
                    });
            }

            return new JObject
                {
                    { Tokens.ErrorsToken, new JArray() },
                    { Tokens.ElementsToken, elements }
                };
        }
    }
}

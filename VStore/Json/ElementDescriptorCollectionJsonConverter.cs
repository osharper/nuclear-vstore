using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Json
{
    public sealed class ElementDescriptorCollectionJsonConverter : JsonConverter
    {
        public override bool CanWrite => false;

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException();
        }

        public override bool CanConvert(Type objectType) => objectType == typeof(IReadOnlyCollection<IElementDescriptor>);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var array = JArray.Load(reader);

            var elementDescriptors = new List<IElementDescriptor>();
            foreach (var descriptor in array)
            {
                var elementDescriptor = descriptor.ToObject<IElementDescriptor>(serializer);
                if (elementDescriptor == null)
                {
                    return Array.Empty<IElementDescriptor>();
                }

                elementDescriptors.Add(elementDescriptor);
            }

            return elementDescriptors;
        }
    }
}

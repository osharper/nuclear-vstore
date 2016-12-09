using System.Globalization;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

namespace NuClear.VStore.Json
{
    public static class SerializerSettings
    {
        static SerializerSettings()
        {
            Default = new JsonSerializerSettings
                          {
                              Culture = CultureInfo.InvariantCulture,
                              ContractResolver = new CamelCasePropertyNamesContractResolver()
                          };
            Default.Converters.Insert(0, new StringEnumConverter { CamelCaseText = true });
            Default.Converters.Insert(1, new ElementDescriptorJsonConverter());
            Default.Converters.Insert(2, new ElementDescriptorCollectionJsonConverter());
            Default.Converters.Insert(3, new TemplateDescriptorJsonConverter());
            Default.Converters.Insert(4, new ObjectElementDescriptorJsonConverter());
            Default.Converters.Insert(5, new ObjectDescriptorJsonConverter());
        }

        public static JsonSerializerSettings Default { get; }
    }
}
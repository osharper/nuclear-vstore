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
            Default.Converters.Insert(1, new TemplateDescriptorJsonConverter());
            Default.Converters.Insert(2, new ObjectElementDescriptorJsonConverter());
        }

        public static JsonSerializerSettings Default { get; }
    }
}
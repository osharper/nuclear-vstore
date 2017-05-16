using System.Globalization;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using NuClear.VStore.Json;

namespace MigrationTool.Json
{
    internal static class ApiSerializerSettings
    {
        static ApiSerializerSettings()
        {
            Default = new JsonSerializerSettings
                {
                    Culture = CultureInfo.InvariantCulture,
                    ContractResolver = new CamelCasePropertyNamesContractResolver()
                };
            Default.Converters.Insert(0, new StringEnumConverter { CamelCaseText = true });
            Default.Converters.Insert(1, new ElementDescriptorJsonConverter());
            Default.Converters.Insert(2, new ElementDescriptorCollectionJsonConverter());
            Default.Converters.Insert(3, new ApiObjectElementDescriptorJsonConverter());
            Default.Converters.Insert(4, new ApiObjectDescriptorJsonConverter());
        }

        public static JsonSerializerSettings Default { get; }
    }
}

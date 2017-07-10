using Newtonsoft.Json;

using NuClear.VStore.Json;

namespace NuClear.VStore.Events
{
    public static class Event
    {
        public static TEvent Deserialize<TEvent>(string value) where TEvent : IEvent
            => JsonConvert.DeserializeObject<TEvent>(value, SerializerSettings.Default);
    }
}
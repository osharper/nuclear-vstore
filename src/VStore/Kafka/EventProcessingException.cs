using System;

namespace NuClear.VStore.Kafka
{
    public sealed class EventProcessingException : Exception
    {
        public EventProcessingException(string message) : base(message)
        {
        }
    }
}
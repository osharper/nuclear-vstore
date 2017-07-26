using System;

namespace NuClear.VStore.Kafka
{
    public sealed class EventReceivingException : Exception
    {
        public EventReceivingException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
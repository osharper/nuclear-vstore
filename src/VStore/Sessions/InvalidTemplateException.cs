using System;

namespace NuClear.VStore.Sessions
{
    public sealed class InvalidTemplateException : Exception
    {
        public InvalidTemplateException(string message) : base(message)
        {
        }
    }
}
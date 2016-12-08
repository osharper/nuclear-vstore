using System;

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class InvalidHtmlException : Exception
    {
        public InvalidHtmlException() : base("Html is invalid")
        {
        }
    }
}

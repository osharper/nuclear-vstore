using System;

namespace NuClear.VStore.Objects.Validate.Exceptions
{
    public class ControlСharactersInTextException : Exception
    {
        public ControlСharactersInTextException() : base("Control characters found")
        {
        }
    }
}

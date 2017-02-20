using System;

namespace NuClear.VStore.Templates
{
    public sealed class TemplateInconsistentException : Exception
    {
        public TemplateInconsistentException(int templateCode, string details)
            : base($"Template element with templateCode {templateCode} is inconsistent. Details: {details}")
        {
        }
    }
}

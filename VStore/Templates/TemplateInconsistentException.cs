using System;

namespace NuClear.VStore.Templates
{
    public sealed class TemplateInconsistentException : Exception
    {
        public TemplateInconsistentException(string templateId, string details)
            : base($"Template '{templateId}' is inconsistent. Details: {details}")
        {
        }
    }
}
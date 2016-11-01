using System;

namespace NuClear.VStore.Templates
{
    public sealed class TemplateInconsistentException : Exception
    {
        public TemplateInconsistentException(long? templateId, string details)
            : base(templateId.HasValue ? $"Template '{templateId}' is inconsistent. Details: {details}" : details)
        {
        }
    }
}
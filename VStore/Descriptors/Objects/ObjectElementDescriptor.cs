using System;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors.Objects
{
    public sealed class ObjectElementDescriptor : IVersionedObjectElementDescriptor
    {
        private readonly IElementDescriptor _elementDescriptor;

        public ObjectElementDescriptor(IElementDescriptor elementDescriptor, IObjectElementValue value)
        {
            _elementDescriptor = elementDescriptor;
            Value = value;
        }

        public long Id { get; set; }
        public string VersionId { get; set; }
        public DateTime LastModified { get; set; }
        public ElementDescriptorType Type => _elementDescriptor.Type;
        public int TemplateCode => _elementDescriptor.TemplateCode;
        public JObject Properties => _elementDescriptor.Properties;
        public IConstraintSet Constraints => _elementDescriptor.Constraints;
        public IObjectElementValue Value { get; }
    }
}
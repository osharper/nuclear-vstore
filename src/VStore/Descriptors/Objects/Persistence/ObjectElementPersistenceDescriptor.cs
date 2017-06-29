using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors.Objects.Persistence
{
    public sealed class ObjectElementPersistenceDescriptor : IObjectElementPersistenceDescriptor
    {
        private readonly IElementDescriptor _elementDescriptor;

        public ObjectElementPersistenceDescriptor(IElementDescriptor elementDescriptor, IObjectElementValue value)
        {
            _elementDescriptor = elementDescriptor;
            Value = value;
        }

        public ElementDescriptorType Type => _elementDescriptor.Type;
        public int TemplateCode => _elementDescriptor.TemplateCode;
        public JObject Properties => _elementDescriptor.Properties;
        public ConstraintSet Constraints => _elementDescriptor.Constraints;
        public IObjectElementValue Value { get; }
    }
}
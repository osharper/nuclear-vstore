using System;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors.Objects
{
    public sealed class ObjectElementDescriptor : IVersionedObjectElementDescriptor
    {
        private readonly IElementDescriptor _elementDescriptor;

        public ObjectElementDescriptor(long id, string versionId, IElementDescriptor elementDescriptor, IObjectElementValue value)
            : this(id, versionId, DateTime.MinValue, elementDescriptor, value)
        {
        }

        public ObjectElementDescriptor(long id, string versionId, DateTime lastModified, IElementDescriptor elementDescriptor, IObjectElementValue value)
        {
            _elementDescriptor = elementDescriptor;
            Id = id;
            VersionId = versionId;
            LastModified = lastModified;
            Value = value;
        }

        public long Id { get; }
        public string VersionId { get; }
        public DateTime LastModified { get; }
        public ElementDescriptorType Type => _elementDescriptor.Type;
        public int TemplateCode => _elementDescriptor.TemplateCode;
        public JObject Properties => _elementDescriptor.Properties;
        public IConstraintSet Constraints => _elementDescriptor.Constraints;
        public IObjectElementValue Value { get; }
    }
}
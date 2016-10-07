using System;

using Amazon.S3.Model;

using NuClear.VStore.Descriptors;

namespace NuClear.VStore.s3
{
    public sealed class TemplateDescriptorBuilder
    {
        private readonly Guid _id;

        private string _version;
        private DateTime _lastModified;
        private MetadataCollectionWrapper _metadataCollectionWrapper;

        private TemplateDescriptorBuilder(Guid id)
        {
            _id = id;
        }

        public static TemplateDescriptorBuilder For(string id)
        {
            return new TemplateDescriptorBuilder(Guid.Parse(id));
        }

        public static TemplateDescriptorBuilder For(Guid id)
        {
            return new TemplateDescriptorBuilder(id);
        }

        public TemplateDescriptorBuilder WithVersion(string version)
        {
            _version = version;
            return this;
        }

        public TemplateDescriptorBuilder WithLastModified(DateTime lastModified)
        {
            _lastModified = lastModified;
            return this;
        }

        public TemplateDescriptorBuilder WithMetadata(MetadataCollection metadata)
        {
            _metadataCollectionWrapper = MetadataCollectionWrapper.For(metadata);
            return this;
        }

        public TemplateDescriptor Build()
        {
            return new TemplateDescriptor
                {
                    Id = _id,
                    VersionId = _version,
                    LastModified = _lastModified,
                    Name = _metadataCollectionWrapper.Read<string>(MetadataElement.Name),
                    IsMandatory = _metadataCollectionWrapper.Read<bool>(MetadataElement.IsMandatory)
                };
        }
    }
}
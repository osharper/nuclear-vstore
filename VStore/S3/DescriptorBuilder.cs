using System;

using Amazon.S3.Model;

using NuClear.VStore.Descriptors;

namespace NuClear.VStore.S3
{
    public sealed class DescriptorBuilder
    {
        private readonly string _id;

        private string _version;
        private DateTime _lastModified;
        private MetadataCollectionWrapper _metadataCollectionWrapper;

        private DescriptorBuilder(string id)
        {
            _id = id;
        }

        public static DescriptorBuilder For(string id)
        {
            return new DescriptorBuilder(id);
        }

        public static DescriptorBuilder For<TId>(TId id)
        {
            return new DescriptorBuilder(id.ToString());
        }

        public DescriptorBuilder WithVersion(string version)
        {
            _version = version;
            return this;
        }

        public DescriptorBuilder WithLastModified(DateTime lastModified)
        {
            _lastModified = lastModified;
            return this;
        }

        public DescriptorBuilder WithMetadata(MetadataCollection metadata)
        {
            _metadataCollectionWrapper = MetadataCollectionWrapper.For(metadata);
            return this;
        }

        public TDescriptor Build<TDescriptor>() where TDescriptor : IDescriptor
        {
            IDescriptor descriptor = null;
            if (typeof(TDescriptor) == typeof(TemplateDescriptor))
            {
                descriptor = new TemplateDescriptor
                    {
                        Id = new Guid(_id),
                        VersionId = _version,
                        LastModified = _lastModified,
                        Name = _metadataCollectionWrapper.Read<string>(MetadataElement.Name),
                        IsMandatory = _metadataCollectionWrapper.Read<bool>(MetadataElement.IsMandatory)
                    };
            }
            else if (typeof(TDescriptor) == typeof(ContentDescriptor))
            {
                descriptor = new ContentDescriptor
                    {
                        Id = long.Parse(_id),
                        VersionId = _version,
                        LastModified = _lastModified,
                        Name = _metadataCollectionWrapper.Read<string>(MetadataElement.Name)
                    };
            }

            return (TDescriptor)descriptor;
        }
    }
}
using System;

using Newtonsoft.Json.Linq;

using NuClear.VStore.Descriptors.Objects;
using NuClear.VStore.Descriptors.Templates;

namespace NuClear.VStore.Descriptors
{
    public sealed class DescriptorBuilder
    {
        private readonly string _id;

        private string _version;
        private DateTime _lastModified;
        private JObject _properties;

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

        public DescriptorBuilder WithProperties(JObject properties)
        {
            _properties = properties;
            return this;
        }

        public TDescriptor Build<TDescriptor>() where TDescriptor : IDescriptor
        {
            IDescriptor descriptor = null;
            if (typeof(TDescriptor) == typeof(TemplateDescriptor))
            {
                descriptor = new TemplateDescriptor
                    {
                        Id = long.Parse(_id),
                        VersionId = _version,
                        LastModified = _lastModified,
                        Properties = _properties
                    };
            }
            else if (typeof(TDescriptor) == typeof(ObjectDescriptor))
            {
                descriptor = new ObjectDescriptor
                    {
                        Id = long.Parse(_id),
                        VersionId = _version,
                        LastModified = _lastModified
                    };
            }

            return (TDescriptor)descriptor;
        }
    }
}
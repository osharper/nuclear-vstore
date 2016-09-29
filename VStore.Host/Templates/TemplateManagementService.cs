using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;

using Microsoft.Extensions.Options;

using NuClear.VStore.Host.Core;
using NuClear.VStore.Host.Descriptors;
using NuClear.VStore.Host.Locks;
using NuClear.VStore.Host.Options;

namespace NuClear.VStore.Host.Templates
{
    public sealed class TemplateManagementService
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly LockSessionFactory _lockSessionFactory;
        private readonly string _bucketName;

        public TemplateManagementService(
            IOptions<CephOptions> cephOptions,
            IAmazonS3 amazonS3,
            LockSessionFactory lockSessionFactory)
        {
            _amazonS3 = amazonS3;
            _lockSessionFactory = lockSessionFactory;
            _bucketName = cephOptions.Value.TemplatesBucketName;
        }

        public IReadOnlyCollection<IElementDescriptor> GetAvailableElementDescriptors()
        {
            return new IElementDescriptor[]
                {
                    new TextElementDescriptor(),
                    new ImageElementDescriptor(),
                    new ArticleElementDescriptor()
                };
        }

        public async Task<IReadOnlyCollection<TemplateDescriptor>> GetAllTemplateDescriptors()
        {
            var listObjectsResponse = await _amazonS3.ListObjectsAsync(_bucketName);

            var descriptors = new ConcurrentBag<TemplateDescriptor>();
            Parallel.ForEach(
                listObjectsResponse.S3Objects,
                async obj =>
                {
                    var metadataResponse = await _amazonS3.GetObjectMetadataAsync(_bucketName, obj.Key);
                    descriptors.Add(new TemplateDescriptor(Guid.Parse(obj.Key),
                                                           metadataResponse.VersionId,
                                                           metadataResponse.Metadata["name".AsMetadata()],
                                                           Enumerable.Empty<IElementDescriptor>()));
                });

            return descriptors;
        }

        public async Task CreateTemplate(TemplateDescriptor templateDescriptor)
        {
            using (_lockSessionFactory.CreateLockSession(templateDescriptor.Id))
            {
                
            }
        }
    }
}
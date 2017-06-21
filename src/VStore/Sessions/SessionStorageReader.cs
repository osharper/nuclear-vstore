using System.Net;
using System.Threading.Tasks;

using Amazon.S3;

using NuClear.VStore.Descriptors.Sessions;
using NuClear.VStore.S3;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionStorageReader
    {
        private readonly string _filesBucketName;
        private readonly IAmazonS3 _amazonS3;

        public SessionStorageReader(string filesBucketName, IAmazonS3 amazonS3)
        {
            _filesBucketName = filesBucketName;
            _amazonS3 = amazonS3;
        }

        public async Task<BinaryMetadata> GetBinaryMetadata(string key)
        {
            try
            {
                var metadataResponse = await _amazonS3.GetObjectMetadataAsync(_filesBucketName, key);
                var metadataWrapper = MetadataCollectionWrapper.For(metadataResponse.Metadata);
                var filename = metadataWrapper.Read<string>(MetadataElement.Filename);

                return new BinaryMetadata(filename, metadataResponse.ContentLength);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new ObjectNotFoundException($"Binary with the key '{key}' not found.");
            }
        }
    }
}

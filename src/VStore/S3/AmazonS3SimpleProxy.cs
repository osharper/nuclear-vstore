using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

namespace NuClear.VStore.S3
{
    public class AmazonS3SimpleProxy : IAmazonS3Proxy
    {
        private readonly IAmazonS3 _amazonS3;

        public AmazonS3SimpleProxy(IAmazonS3 amazonS3)
        {
            _amazonS3 = amazonS3;
        }

        public async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request) =>
            await _amazonS3.PutObjectAsync(request);

        public async Task<ListObjectsResponse> ListObjectsAsync(ListObjectsRequest request) =>
            await _amazonS3.ListObjectsAsync(request);

        public async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key) =>
            await _amazonS3.GetObjectMetadataAsync(bucketName, key);

        public async Task<GetObjectResponse> GetObjectAsync(string bucketName, string key, string versionId) =>
            await _amazonS3.GetObjectAsync(bucketName, key, versionId);

        public async Task<ListVersionsResponse> ListVersionsAsync(string bucketName, string key) =>
            await _amazonS3.ListVersionsAsync(bucketName, key);

        public async Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request) =>
            await _amazonS3.ListObjectsV2Async(request);

        public async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, string versionId) =>
            await _amazonS3.GetObjectMetadataAsync(bucketName, key, versionId);

        public async Task<ListVersionsResponse> ListVersionsAsync(ListVersionsRequest request) =>
            await _amazonS3.ListVersionsAsync(request);

        public async Task<GetObjectResponse> GetObjectAsync(string bucketName, string key) =>
            await _amazonS3.GetObjectAsync(bucketName, key);

        public async Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key, string versionId) =>
            await _amazonS3.DeleteObjectAsync(bucketName, key, versionId);
    }
}

using System.Threading.Tasks;

using Amazon.S3.Model;

namespace NuClear.VStore.S3
{
    public abstract class S3ClientProxy : IS3Client
    {
        private readonly IS3Client _s3Client;

        protected S3ClientProxy(IS3Client s3Client)
        {
            _s3Client = s3Client;
        }

        public async Task<ListObjectsResponse> ListObjectsAsync(ListObjectsRequest request)
            => await _s3Client.ListObjectsAsync(request);

        public async Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request)
            => await _s3Client.ListObjectsV2Async(request);

        public async Task<ListVersionsResponse> ListVersionsAsync(string bucketName, string key)
            => await _s3Client.ListVersionsAsync(bucketName, key);

        public async Task<ListVersionsResponse> ListVersionsAsync(ListVersionsRequest request)
            => await _s3Client.ListVersionsAsync(request);

        public async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key)
            => await _s3Client.GetObjectMetadataAsync(bucketName, key);

        public async Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, string versionId)
            => await _s3Client.GetObjectMetadataAsync(bucketName, key, versionId);

        public async Task<GetObjectResponse> GetObjectAsync(string bucketName, string key)
            => await _s3Client.GetObjectAsync(bucketName, key);

        public async Task<GetObjectResponse> GetObjectAsync(string bucketName, string key, string versionId)
            => await _s3Client.GetObjectAsync(bucketName, key, versionId);

        public async Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request)
            => await _s3Client.PutObjectAsync(request);

        public async Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key)
            => await _s3Client.DeleteObjectAsync(bucketName, key);

        public async Task<CopyObjectResponse> CopyObjectAsync(CopyObjectRequest request)
            => await _s3Client.CopyObjectAsync(request);
    }
}
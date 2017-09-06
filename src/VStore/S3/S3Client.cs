using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

namespace NuClear.VStore.S3
{
    public sealed class S3Client : IS3Client
    {
        private readonly IAmazonS3 _amazonS3;

        public S3Client(IAmazonS3 amazonS3)
        {
            _amazonS3 = amazonS3;
        }

        async Task<ListObjectsResponse> IS3Client.ListObjectsAsync(ListObjectsRequest request)
            => await _amazonS3.ListObjectsAsync(request);

        async Task<ListObjectsV2Response> IS3Client.ListObjectsV2Async(ListObjectsV2Request request)
            => await _amazonS3.ListObjectsV2Async(request);

        async Task<ListVersionsResponse> IS3Client.ListVersionsAsync(string bucketName, string key)
            => await _amazonS3.ListVersionsAsync(bucketName, key);

        async Task<ListVersionsResponse> IS3Client.ListVersionsAsync(ListVersionsRequest request)
            => await _amazonS3.ListVersionsAsync(request);

        async Task<GetObjectMetadataResponse> IS3Client.GetObjectMetadataAsync(string bucketName, string key)
            => await _amazonS3.GetObjectMetadataAsync(bucketName, key);

        async Task<GetObjectMetadataResponse> IS3Client.GetObjectMetadataAsync(string bucketName, string key, string versionId)
            => await _amazonS3.GetObjectMetadataAsync(bucketName, key, versionId);

        async Task<GetObjectResponse> IS3Client.GetObjectAsync(string bucketName, string key)
            => await _amazonS3.GetObjectAsync(bucketName, key);

        async Task<GetObjectResponse> IS3Client.GetObjectAsync(string bucketName, string key, string versionId)
            => await _amazonS3.GetObjectAsync(bucketName, key, versionId);

        async Task<PutObjectResponse> IS3Client.PutObjectAsync(PutObjectRequest request)
            => await _amazonS3.PutObjectAsync(request);

        async Task<DeleteObjectResponse> IS3Client.DeleteObjectAsync(string bucketName, string key)
            => await _amazonS3.DeleteObjectAsync(bucketName, key);

        async Task<DeleteObjectResponse> IS3Client.DeleteObjectAsync(string bucketName, string key, string versionId)
            => await _amazonS3.DeleteObjectAsync(bucketName, key, versionId);

        async Task<CopyObjectResponse> IS3Client.CopyObjectAsync(CopyObjectRequest request)
            => await _amazonS3.CopyObjectAsync(request);
    }
}
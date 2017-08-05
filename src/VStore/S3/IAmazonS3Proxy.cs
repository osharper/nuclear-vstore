using System.Threading.Tasks;

using Amazon.S3.Model;

namespace NuClear.VStore.S3
{
    public interface IAmazonS3Proxy
    {
        Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request);
        Task<ListObjectsResponse> ListObjectsAsync(ListObjectsRequest request);
        Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key);
        Task<GetObjectResponse> GetObjectAsync(string bucketName, string key, string versionId);
        Task<ListVersionsResponse> ListVersionsAsync(string bucketName, string key);
        Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request);
        Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, string versionId);
        Task<ListVersionsResponse> ListVersionsAsync(ListVersionsRequest request);
        Task<GetObjectResponse> GetObjectAsync(string bucketName, string key);
        Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key, string versionId);
        Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key);
    }
}

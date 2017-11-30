using System.Threading.Tasks;

using Amazon.S3.Model;

namespace NuClear.VStore.S3
{
    public interface IS3Client
    {
        Task<ListObjectsResponse> ListObjectsAsync(ListObjectsRequest request);
        Task<ListObjectsV2Response> ListObjectsV2Async(ListObjectsV2Request request);
        Task<ListVersionsResponse> ListVersionsAsync(string bucketName, string key);
        Task<ListVersionsResponse> ListVersionsAsync(ListVersionsRequest request);
        Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key);
        Task<GetObjectMetadataResponse> GetObjectMetadataAsync(string bucketName, string key, string versionId);
        Task<GetObjectResponse> GetObjectAsync(string bucketName, string key);
        Task<GetObjectResponse> GetObjectAsync(string bucketName, string key, string versionId);
        Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request);
        Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key);
        Task<CopyObjectResponse> CopyObjectAsync(CopyObjectRequest request);
    }
}
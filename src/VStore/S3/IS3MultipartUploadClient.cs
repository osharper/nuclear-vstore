using System.Threading.Tasks;

using Amazon.S3.Model;

namespace NuClear.VStore.S3
{
    public interface IS3MultipartUploadClient
    {
        Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(InitiateMultipartUploadRequest request);
        Task<UploadPartResponse> UploadPartAsync(UploadPartRequest request);
        Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(CompleteMultipartUploadRequest request);
        Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(string bucketName, string key, string uploadId);
    }
}
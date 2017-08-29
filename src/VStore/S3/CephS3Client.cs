using System.Threading.Tasks;

using Amazon.S3.Model;

namespace NuClear.VStore.S3
{
    public sealed class CephS3Client : S3ClientProxy, ICephS3Client
    {
        private readonly IS3MultipartUploadClient _s3MultipartUploadClient;

        public CephS3Client(IS3Client s3Client, IS3MultipartUploadClient s3MultipartUploadClient) : base(s3Client)
        {
            _s3MultipartUploadClient = s3MultipartUploadClient;
        }

        public async Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(InitiateMultipartUploadRequest request)
            => await _s3MultipartUploadClient.InitiateMultipartUploadAsync(request);

        public async Task<UploadPartResponse> UploadPartAsync(UploadPartRequest request)
            => await _s3MultipartUploadClient.UploadPartAsync(request);

        public async Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(CompleteMultipartUploadRequest request)
            => await _s3MultipartUploadClient.CompleteMultipartUploadAsync(request);

        public async Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(string bucketName, string key, string uploadId)
            => await _s3MultipartUploadClient.AbortMultipartUploadAsync(bucketName, key, uploadId);
    }
}
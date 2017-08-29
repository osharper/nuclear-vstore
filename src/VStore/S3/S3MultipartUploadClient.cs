using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

namespace NuClear.VStore.S3
{
    public sealed class S3MultipartUploadClient : IS3MultipartUploadClient
    {
        private readonly IAmazonS3 _amazonS3;

        public S3MultipartUploadClient(IAmazonS3 amazonS3)
        {
            _amazonS3 = amazonS3;
        }

        public async Task<InitiateMultipartUploadResponse> InitiateMultipartUploadAsync(InitiateMultipartUploadRequest request)
            => await _amazonS3.InitiateMultipartUploadAsync(request);

        public async Task<UploadPartResponse> UploadPartAsync(UploadPartRequest request)
            => await _amazonS3.UploadPartAsync(request);

        public async Task<CompleteMultipartUploadResponse> CompleteMultipartUploadAsync(CompleteMultipartUploadRequest request)
            => await _amazonS3.CompleteMultipartUploadAsync(request);

        public async Task<AbortMultipartUploadResponse> AbortMultipartUploadAsync(string bucketName, string key, string uploadId)
            => await _amazonS3.AbortMultipartUploadAsync(bucketName, key, uploadId);
    }
}
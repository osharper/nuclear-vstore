using Amazon.S3;

namespace NuClear.VStore.S3
{
    public sealed class AmazonS3Client : S3ClientProxy, IAmazonS3Client
    {
        public AmazonS3Client(IS3Client s3Client) : base(s3Client)
        {
        }
    }
}
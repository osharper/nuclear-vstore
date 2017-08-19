using System.Threading.Tasks;

using Amazon.S3.Model;

namespace NuClear.VStore.S3
{
    public interface ICephS3Client : IS3Client, IS3MultipartUploadClient
    {
    }
}
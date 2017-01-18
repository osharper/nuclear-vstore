using System;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

namespace NuClear.VStore.Sessions
{
    public sealed class SessionStorageReader
    {
        private readonly string _filesBucketName;
        private readonly IAmazonS3 _amazonS3;

        public SessionStorageReader(string filesBucketName, IAmazonS3 amazonS3)
        {
            _filesBucketName = filesBucketName;
            _amazonS3 = amazonS3;
        }

        public async Task<bool> IsBinaryExists(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentNullException(nameof(key), "Binary key is not specified");
            }

            var listResponse = await _amazonS3.ListObjectsV2Async(
                                   new ListObjectsV2Request
                                       {
                                           BucketName = _filesBucketName,
                                           Prefix = key,
                                           MaxKeys = 1
                                       });

            var obj = listResponse.S3Objects.SingleOrDefault();
            return obj != null && obj.Key.Equals(key, StringComparison.OrdinalIgnoreCase);
        }
    }
}

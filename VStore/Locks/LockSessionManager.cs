using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using NuClear.VStore.Options;

namespace NuClear.VStore.Locks
{
    public sealed class LockSessionManager
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName;

        public LockSessionManager(IAmazonS3 amazonS3, LockOptions lockOptions)
        {
            _amazonS3 = amazonS3;
            _bucketName = lockOptions.BucketName;
        }

        public async Task<IReadOnlyCollection<string>> GetAllCurrentLockSessions()
        {
            var response = await _amazonS3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucketName });
            return response.S3Objects.Select(x => x.Key).ToArray();
        }

        public async Task DeleteLockSession(string rootObjectKey)
        {
            await _amazonS3.DeleteObjectAsync(new DeleteObjectRequest
                                                  {
                                                      BucketName = _bucketName,
                                                      Key = rootObjectKey
                                                  });
        }
    }
}
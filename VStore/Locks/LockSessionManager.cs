using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using NuClear.VStore.Options;
using NuClear.VStore.S3;

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

        public async Task<IReadOnlyCollection<long>> GetAllCurrentLockSessions()
        {
            var response = await _amazonS3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucketName });
            return response.S3Objects.Select(x => x.Key.AsLockObjectId()).ToArray();
        }

        public async Task DeleteLockSession(long rootObjectKey)
        {
            var lockId = rootObjectKey.AsS3LockKey();
            try
            {
                var responseTask = await _amazonS3.ListVersionsAsync(
                    new ListVersionsRequest
                    {
                        BucketName = _bucketName,
                        Prefix = lockId
                    });

                foreach (var version in responseTask.Versions)
                {
                    await _amazonS3.DeleteObjectAsync(_bucketName, lockId, version.VersionId);
                }
            }
            catch (AmazonS3Exception ex)
            {
                throw new S3Exception(ex);
            }
        }
    }
}

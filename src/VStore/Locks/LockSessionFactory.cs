using System;
using System.Threading.Tasks;

using Amazon.S3;

using NuClear.VStore.Options;

namespace NuClear.VStore.Locks
{
    public sealed class LockSessionFactory
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName;
        private readonly TimeSpan _expiration;

        public LockSessionFactory(IAmazonS3 amazonS3, LockOptions lockOptions)
        {
            _amazonS3 = amazonS3;
            _bucketName = lockOptions.BucketName;
            _expiration = lockOptions.Expiration;
        }

        public async Task<LockSession> CreateLockSessionAsync(long rootObjectId)
            => await LockSession.CreateAsync(_amazonS3, _bucketName, rootObjectId, DateTime.UtcNow.Add(_expiration));
    }
}

using System;

using Amazon.S3;

using NuClear.VStore.Options;

namespace NuClear.VStore.Locks
{
    public sealed class LockSessionFactory
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName;
        private readonly DateTime _expirationDate;

        public LockSessionFactory(IAmazonS3 amazonS3, LockOptions lockOptions)
        {
            _amazonS3 = amazonS3;
            _bucketName = lockOptions.BucketName;
            _expirationDate = DateTime.UtcNow.Add(lockOptions.Expiration);
        }

        public LockSession CreateLockSession(long rootObjectId) => new LockSession(_amazonS3, _bucketName, rootObjectId, _expirationDate);
    }
}

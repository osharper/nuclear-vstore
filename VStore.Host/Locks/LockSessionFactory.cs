using System;

using Amazon.S3;

using Microsoft.Extensions.Options;

using NuClear.VStore.Host.Options;

namespace NuClear.VStore.Host.Locks
{
    public sealed class LockSessionFactory
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName;
        private readonly DateTime _expirationDate;

        public LockSessionFactory(IAmazonS3 amazonS3, IOptions<LockOptions> lockOptions)
        {
            _amazonS3 = amazonS3;
            _bucketName = lockOptions.Value.BucketName;
            _expirationDate = DateTime.UtcNow.Add(lockOptions.Value.Expiration);
        }

        public LockSession CreateLockSession(string rootObjectKey)
            => new LockSession(_amazonS3, _bucketName, rootObjectKey, _expirationDate);
    }
}
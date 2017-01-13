using System;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;

using NuClear.VStore.S3;

namespace NuClear.VStore.Locks
{
    public sealed class LockSession : IDisposable
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName;
        private readonly string _rootObjectId;

        public LockSession(IAmazonS3 amazonS3, string bucketName, long rootObjectId, DateTime expirationDate)
            : this(amazonS3, bucketName, rootObjectId.ToString(), expirationDate)
        {
        }

        private LockSession(IAmazonS3 amazonS3, string bucketName, string rootObjectId, DateTime expirationDate)
        {
            _rootObjectId = rootObjectId;
            _amazonS3 = amazonS3;
            _bucketName = bucketName;

            EnsureLockNotExists();

            var content = JsonConvert.SerializeObject(new { ExpirationDate = expirationDate });
            CreateSessionLock(content);
        }

        public void Dispose() => _amazonS3.DeleteObjectAsync(_bucketName, _rootObjectId).Wait();

        private void EnsureLockNotExists()
        {
            var responseTask = _amazonS3.ListObjectsV2Async(
                new ListObjectsV2Request
                    {
                        BucketName = _bucketName,
                        Prefix = _rootObjectId
                    });

            if (responseTask.Result.S3Objects.Count > 0)
            {
                throw new SessionLockAlreadyExistsException(_rootObjectId);
            }
        }

        private void CreateSessionLock(string content)
            => _amazonS3.PutObjectAsync(
                            new PutObjectRequest
                                {
                                    BucketName = _bucketName,
                                    Key = _rootObjectId,
                                    ContentType = ContentType.Json,
                                    ContentBody = content,
                                    CannedACL = S3CannedACL.PublicRead
                                })
                        .Wait();
    }
}
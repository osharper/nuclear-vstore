using System;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;

namespace NuClear.VStore.Host.Locks
{
    public sealed class LockSession : IDisposable
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName;

        public LockSession(IAmazonS3 amazonS3, string bucketName, string rootObjectKey, DateTime expirationDate)
        {
            RootObjectKey = rootObjectKey;
            _amazonS3 = amazonS3;
            _bucketName = bucketName;

            EnsureLockNotExist();

            var content = JsonConvert.SerializeObject(new { ExpirationDate = expirationDate });
            CreateSessionLock(content);
        }

        public string RootObjectKey { get; }

        public void Dispose() => _amazonS3.DeleteObjectAsync(_bucketName, RootObjectKey);

        private void EnsureLockNotExist()
        {
            var responseTask = _amazonS3.ListObjectsV2Async(
                new ListObjectsV2Request
                    {
                        BucketName = _bucketName,
                        Prefix = RootObjectKey
                    });

            if (responseTask.Result.S3Objects.Count > 0)
            {
                throw new SessionLockAlreadyExistsException(RootObjectKey);
            }
        }

        private void CreateSessionLock(string content)
            => _amazonS3.PutObjectAsync(
                            new PutObjectRequest
                                {
                                    BucketName = _bucketName,
                                    Key = RootObjectKey,
                                    ContentType = "application/json",
                                    ContentBody = content,
                                    CannedACL = S3CannedACL.PublicRead
                                })
                        .Wait();
    }
}
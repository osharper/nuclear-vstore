using System;
using System.Linq;

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
        private readonly string _lockKey;
        private readonly string _rootObjectId;
        private string _versionId;

        public LockSession(IAmazonS3 amazonS3, string bucketName, long rootObjectId, DateTime expirationDate)
            : this(amazonS3, bucketName, rootObjectId.ToString(), expirationDate)
        {
        }

        private LockSession(IAmazonS3 amazonS3, string bucketName, string rootObjectId, DateTime expirationDate)
        {
            _rootObjectId = rootObjectId;
            _lockKey = rootObjectId + "#";
            _amazonS3 = amazonS3;
            _bucketName = bucketName;

            EnsureLockNotExists();

            var content = JsonConvert.SerializeObject(new { ExpirationDate = expirationDate, UniqueKey = Guid.NewGuid() });
            var response = CreateSessionLock(content);
            EnsureLockIsTaken(response.ETag);
        }

        public void Dispose() => _amazonS3.DeleteObjectAsync(_bucketName, _lockKey, _versionId).Wait();

        private void EnsureLockNotExists()
        {
            try
            {
                using (_amazonS3.GetObjectAsync(
                                       new GetObjectRequest
                                       {
                                           BucketName = _bucketName,
                                           Key = _lockKey
                                       })
                                   .Result)
                {
                }
            }
            catch
            {
                return;
            }

            throw new SessionLockAlreadyExistsException(_rootObjectId);
        }

        private PutObjectResponse CreateSessionLock(string content)
            => _amazonS3.PutObjectAsync(
                            new PutObjectRequest
                            {
                                BucketName = _bucketName,
                                Key = _lockKey,
                                ContentType = ContentType.Json,
                                ContentBody = content,
                                CannedACL = S3CannedACL.PublicRead
                            })
                        .Result;

        private void EnsureLockIsTaken(string tag)
        {
            var responseTask = _amazonS3.ListVersionsAsync(
                new ListVersionsRequest
                {
                    BucketName = _bucketName,
                    Prefix = _lockKey
                });

            var versions = responseTask.Result.Versions;
            _versionId = versions.Single(v => v.ETag == tag).VersionId;
            if (_versionId != versions[versions.Count - 1].VersionId)
            {
                throw new SessionLockAlreadyExistsException(_rootObjectId);
            }
        }
    }
}
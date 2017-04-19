using System;
using System.Linq;
using System.Net;

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

        public LockSession(IAmazonS3 amazonS3, string bucketName, long rootObjectId, DateTime expirationDate)
        {
            _rootObjectId = rootObjectId.ToString();
            _lockKey = rootObjectId.AsS3LockKey();
            _amazonS3 = amazonS3;
            _bucketName = bucketName;

            EnsureLockNotExists();

            var content = JsonConvert.SerializeObject(new { ExpirationDate = expirationDate, UniqueKey = Guid.NewGuid() });
            var response = CreateSessionLock(content);
            EnsureLockIsTaken(response.ETag);
        }

        public void Dispose()
        {
            // List all versions of current lock (there might be newly created):
            var responseTask = _amazonS3.ListVersionsAsync(
                new ListVersionsRequest
                {
                    BucketName = _bucketName,
                    Prefix = _lockKey
                });

            // Clean up versions of current lock:
            foreach (var version in responseTask.Result.Versions)
            {
                _amazonS3.DeleteObjectAsync(_bucketName, _lockKey, version.VersionId).Wait();
            }
        }

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
            catch (AggregateException aex)
            {
                var baseException = aex.GetBaseException() as AmazonS3Exception;
                if (baseException != null && baseException.StatusCode == HttpStatusCode.NotFound)
                {
                    return;
                }

                throw;
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
            var versionId = versions.Single(v => v.ETag == tag).VersionId;
            if (versionId != versions[versions.Count - 1].VersionId)
            {
                throw new SessionLockAlreadyExistsException(_rootObjectId);
            }
        }
    }
}

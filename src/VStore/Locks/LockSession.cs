using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Newtonsoft.Json;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Http;
using NuClear.VStore.Json;
using NuClear.VStore.S3;

namespace NuClear.VStore.Locks
{
    public sealed class LockSession
    {
        private readonly IAmazonS3 _amazonS3;
        private readonly string _bucketName;
        private readonly string _lockKey;
        private readonly string _rootObjectId;

        private LockSession(IAmazonS3 amazonS3, string bucketName, long rootObjectId)
        {
            _rootObjectId = rootObjectId.ToString();
            _lockKey = rootObjectId.AsS3LockKey();
            _amazonS3 = amazonS3;
            _bucketName = bucketName;
        }

        public static async Task<LockSession> CreateAsync(IAmazonS3 amazonS3, string bucketName, long rootObjectId, DateTime expirationDate)
        {
            var lockSession = new LockSession(amazonS3, bucketName, rootObjectId);
            await lockSession.EnsureLockNotExistsAsync();

            var lockSessionDescriptor = new LockSessionDescriptor { ExpirationDate = expirationDate, UniqueKey = Guid.NewGuid() };
            var etag = await lockSession.CreateSessionLockAsync(lockSessionDescriptor);
            await lockSession.EnsureLockIsTakenAsync(etag);

            return lockSession;
        }

        public async Task ReleaseAsync()
        {
            // List all versions of current lock (there might be newly created):
            var response = await _amazonS3.ListVersionsAsync(new ListVersionsRequest { BucketName = _bucketName, Prefix = _lockKey });

            // Clean up versions of current lock:
            foreach (var version in response.Versions)
            {
                await _amazonS3.DeleteObjectAsync(_bucketName, _lockKey, version.VersionId);
            }
        }

        private async Task EnsureLockNotExistsAsync()
        {
            var response = await _amazonS3.ListVersionsAsync(new ListVersionsRequest { BucketName = _bucketName, Prefix = _lockKey });
            if (response.Versions.Count > 0)
            {
                throw new SessionLockAlreadyExistsException(_rootObjectId);
            }
        }

        private async Task<string> CreateSessionLockAsync(LockSessionDescriptor lockSessionDescriptor)
        {
            var json = JsonConvert.SerializeObject(lockSessionDescriptor, SerializerSettings.Default);
            var response = await _amazonS3.PutObjectAsync(
                               new PutObjectRequest
                                   {
                                       BucketName = _bucketName,
                                       Key = _lockKey,
                                       ContentType = ContentType.Json,
                                       ContentBody = json,
                                       CannedACL = S3CannedACL.PublicRead
                                   });

            return response.ETag;
        }

        private async Task EnsureLockIsTakenAsync(string etag)
        {
            var response = await _amazonS3.ListVersionsAsync(new ListVersionsRequest { BucketName = _bucketName, Prefix = _lockKey });
            var versions = response.Versions;

            var versionId = versions.Single(v => v.ETag == etag).VersionId;
            if (versionId != versions[versions.Count - 1].VersionId)
            {
                throw new SessionLockAlreadyExistsException(_rootObjectId);
            }
        }
    }
}

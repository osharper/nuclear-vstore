using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Amazon.S3;
using Amazon.S3.Model;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using NuClear.VStore.Descriptors;
using NuClear.VStore.Http;
using NuClear.VStore.Json;
using NuClear.VStore.Options;
using NuClear.VStore.S3;

namespace NuClear.VStore.Locks
{
    public sealed class LockSessionManager
    {
        private readonly IS3Client _s3Client;
        private readonly ILogger<LockSessionManager> _logger;
        private readonly string _bucketName;
        private readonly TimeSpan _expiration;

        public LockSessionManager(IS3Client s3Client, LockOptions lockOptions, ILogger<LockSessionManager> logger)
        {
            _s3Client = s3Client;
            _logger = logger;
            _bucketName = lockOptions.BucketName;
            _expiration = lockOptions.Expiration;
        }

        public async Task<IReadOnlyCollection<long>> GetAllCurrentLockSessionsAsync()
        {
            var response = await _s3Client.ListObjectsV2Async(new ListObjectsV2Request { BucketName = _bucketName });
            return response.S3Objects.Select(x => x.Key.AsLockObjectId()).ToArray();
        }

        public async Task EnsureLockSessionNotExists(long rootObjectKey)
        {
            var lockId = rootObjectKey.AsS3LockKey();
            var response = await _s3Client.ListVersionsAsync(new ListVersionsRequest { BucketName = _bucketName, Prefix = lockId });
            if (response.Versions.Count > 0)
            {
                throw new LockAlreadyExistsException(rootObjectKey);
            }
        }

        public async Task<LockSession> CreateLockSessionAsync(long rootObjectKey)
        {
            await EnsureLockSessionNotExists(rootObjectKey);

            var lockSession = new LockSession(rootObjectKey, this);
            var lockSessionDescriptor = lockSession.CreateDescriptor(_expiration);

            var lockId = rootObjectKey.AsS3LockKey();
            var json = JsonConvert.SerializeObject(lockSessionDescriptor, SerializerSettings.Default);
            var putObjectResponse = await _s3Client.PutObjectAsync(
                                        new PutObjectRequest
                                            {
                                                BucketName = _bucketName,
                                                Key = lockId,
                                                ContentType = ContentType.Json,
                                                ContentBody = json,
                                                CannedACL = S3CannedACL.PublicRead
                                            });

            var listVersionsResponse = await _s3Client.ListVersionsAsync(new ListVersionsRequest { BucketName = _bucketName, Prefix = lockId });
            var versions = listVersionsResponse.Versions;

            var versionId = versions.Single(v => v.ETag == putObjectResponse.ETag).VersionId;
            if (versionId != versions[versions.Count - 1].VersionId)
            {
                throw new LockAlreadyExistsException(rootObjectKey);
            }

            return lockSession;
        }

        public async Task<bool> IsLockSessionExpired(long rootObjectKey)
        {
            var lockSessionDescriptor = await GetLockSessionDescriptor(rootObjectKey.AsS3LockKey());
            var isExpired = lockSessionDescriptor?.ExpirationDate <= DateTime.UtcNow;
            if (isExpired)
            {
                _logger.LogWarning("Expired lock session found for object with id = {id}.", rootObjectKey);
            }

            return isExpired;
        }

        public async Task DeleteLockSessionAsync(long rootObjectKey)
        {
            var lockId = rootObjectKey.AsS3LockKey();
            try
            {
                var response = await _s3Client.ListVersionsAsync(new ListVersionsRequest { BucketName = _bucketName, Prefix = lockId });
                foreach (var version in response.Versions)
                {
                    await _s3Client.DeleteObjectAsync(_bucketName, lockId, version.VersionId);
                }
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode != HttpStatusCode.NotFound)
            {
                throw new S3Exception(ex);
            }
        }

        private async Task<LockSessionDescriptor> GetLockSessionDescriptor(string key)
        {
            string content;
            try
            {
                using (var getObjectResponse = await _s3Client.GetObjectAsync(_bucketName, key))
                {
                    using (var reader = new StreamReader(getObjectResponse.ResponseStream, Encoding.UTF8))
                    {
                        content = reader.ReadToEnd();
                    }
                }
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<LockSessionDescriptor>(content, SerializerSettings.Default);
        }
    }
}
